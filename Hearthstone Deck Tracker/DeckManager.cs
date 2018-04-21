using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Importing.Game;
using Hearthstone_Deck_Tracker.Importing.Game.ImportOptions;
using Hearthstone_Deck_Tracker.Utility.Logging;
using HearthSim.Core.Hearthstone.Entities;
using Card = Hearthstone_Deck_Tracker.Hearthstone.Card;
using Deck = Hearthstone_Deck_Tracker.Hearthstone.Deck;
using DeckType = HearthSim.Core.Hearthstone.Enums.DeckType;

namespace Hearthstone_Deck_Tracker
{
	public class DeckManager
	{
		private static List<IGrouping<string, Entity>> RevealedEntites => Core.Hearthstone.CurrentGame.LocalPlayer.RevealedCards
			.Where(x => !x.IsCreated && !x.Info.Stolen && (x.Card.Data?.Collectible ?? false)).GroupBy(x => x.CardId)
			.ToList();

		private static List<IGrouping<string, Entity>> GetMissingCards(List<IGrouping<string, Entity>> revealed, Deck deck) =>
			revealed.Where(x => !deck.GetSelectedDeckVersion().Cards.Any(c => c.Id == x.Key && c.Count >= x.Count())).ToList();

		public static void ImportDecks(IEnumerable<ImportedDeck> decks, bool brawl, bool importNew = true, bool updateExisting = true, bool select = true)
		{
			var imported = ImportDecksTo(DeckList.Instance.Decks, decks, brawl, importNew, updateExisting);
			if(!imported.Any())
				return;
			DeckList.Save();
			Core.MainWindow.DeckPickerList.UpdateDecks();
			Core.MainWindow.UpdateIntroLabelVisibility();
			if(select)
				Core.MainWindow.SelectDeck(imported.First(), true);
			Core.UpdatePlayerCards(true);
		}

		public static List<Deck> ImportDecksTo(ICollection<Deck> targetList, IEnumerable<ImportedDeck> decks, bool brawl, bool importNew, bool updateExisting)
		{
			var importedDecks = new List<Deck>();
			foreach(var deck in decks)
			{
				if(deck.SelectedImportOption is NewDeck)
				{
					if(!importNew)
						continue;
					Log.Info($"Saving {deck.Deck.Name} as new deck.");
					var newDeck = new Deck
					{
						Class = deck.Class,
						Name = deck.Deck.Name,
						HsId = deck.Deck.Id,
						Cards = new ObservableCollection<Card>(deck.Deck.Cards.Select(x =>
						{
							var card = Database.GetCardFromId(x.Id);
							card.Count = x.Count;
							return card;
						})),
						LastEdited = DateTime.Now,
						IsArenaDeck = false
					};
					if(brawl)
					{
						newDeck.Tags.Add("Brawl");
						newDeck.Name = Helper.ParseDeckNameTemplate(Config.Instance.BrawlDeckNameTemplate, newDeck);
					}

					var existingWithId = targetList.FirstOrDefault(d => d.HsId == deck.Deck.Id);
					if(existingWithId != null)
						existingWithId.HsId = 0;

					targetList.Add(newDeck);
					importedDecks.Add(newDeck);
				}
				else
				{
					if(!updateExisting)
						continue;
					var existing = deck.SelectedImportOption as ExistingDeck;
					if(existing == null)
						continue;
					var target = existing.Deck;
					target.HsId = deck.Deck.Id;
					if(brawl && !target.Tags.Any(x => x.ToUpper().Contains("BRAWL")))
						target.Tags.Add("Brawl");
					if(target.Archived)
					{
						target.Archived = false;
						Log.Info($"Unarchiving deck: {deck.Deck.Name}.");
					}
					if(existing.NewVersion.Major == 0)
						Log.Info($"Assinging id to existing deck: {deck.Deck.Name}.");
					else
					{
						Log.Info(
							$"Saving {deck.Deck.Name} as {existing.NewVersion.ShortVersionString} (prev={target.Version.ShortVersionString}).");
						targetList.Remove(target);
						var oldDeck = (Deck) target.Clone();
						oldDeck.Versions = new List<Deck>();
						if(!brawl)
							target.Name = deck.Deck.Name;
						target.LastEdited = DateTime.Now;
						target.Versions.Add(oldDeck);
						target.Version = existing.NewVersion;
						target.SelectedVersion = existing.NewVersion;
						target.Cards.Clear();
						var cards = deck.Deck.Cards.Select(x =>
						{
							var card = Database.GetCardFromId(x.Id);
							card.Count = x.Count;
							return card;
						});
						foreach(var card in cards)
							target.Cards.Add(card);
						var clone = (Deck) target.Clone();
						targetList.Add(clone);
						importedDecks.Add(clone);
					}
				}
			}
			return importedDecks;
		}

		public static void SaveDeck(Deck deck, bool invokeApi = true)
		{
			deck.Edited();
			DeckList.Instance.Decks.Add(deck);
			DeckList.Save();
			Core.MainWindow.DeckPickerList.SelectDeckAndAppropriateView(deck);
			Core.MainWindow.DeckPickerList.UpdateDecks(forceUpdate: new[] { deck });
			Core.MainWindow.SelectDeck(deck, true);
			if(invokeApi)
				DeckManagerEvents.OnDeckCreated.Execute(deck);
		}

		public static void SaveDeck(Deck baseDeck, Deck newVersion, bool overwriteCurrent = false)
		{
			DeckList.Instance.Decks.Remove(baseDeck);
			baseDeck.Versions?.Clear();
			if(!overwriteCurrent)
				newVersion.Versions.Add(baseDeck);
			newVersion.SelectedVersion = newVersion.Version;
			newVersion.Archived = false;
			SaveDeck(newVersion, false);
			DeckManagerEvents.OnDeckUpdated.Execute(newVersion);
		}

		public static void DungeonRunMatchStarted(bool newRun, HearthSim.Core.Hearthstone.Deck deck)
		{
			if(!Config.Instance.DungeonAutoImport)
				return;
			Log.Info($"Dungeon run detected! New={newRun}");
			if(newRun && deck != null)
				CreateDungeonDeck(deck);
			else if(deck == null)
			{
				var playerClass = Core.Hearthstone.CurrentGame.LocalPlayer.CurrentHero?.Class ?? CardClass.INVALID;
				var revealed = RevealedEntites;
				var existingDeck = DeckList.Instance.Decks
					.Where(x => x.IsDungeonDeck && playerClass == x.CardClass && !(x.IsDungeonRunCompleted ?? false)
								&& (!newRun || x.Cards.Count == 10) && GetMissingCards(revealed, x).Count == 0)
					.OrderByDescending(x => x.LastEdited).FirstOrDefault();
				if(existingDeck == null)
				{
					Log.Info("We don't have an existing deck for this run");
					var hero = Core.Game.Opponent.PlayerEntities.FirstOrDefault(x => x.IsHero)?.CardId;
					var set = Database.GetCardFromId(hero)?.CardSet;
					CreateDungeonDeck(playerClass, set ?? CardSet.INVALID);
				}
					if(DeckList.Instance.ActiveDeck != null)
					{
						Log.Info("Switching to no deck mode");
						Core.MainWindow.SelectDeck(null, true);
					}
				}
				else if(!existingDeck.Equals(DeckList.Instance.ActiveDeck))
				{
					Log.Info($"Selecting existing deck: {existingDeck.Name}");
					Core.MainWindow.SelectDeck(existingDeck, true);
					if(Core.Hearthstone.CurrentGame?.LocalPlayer != null)
						Core.Hearthstone.CurrentGame.LocalPlayer.Deck = new HearthSim.Core.Hearthstone.Deck(DeckType.DungeonRun,
							existingDeck.Name, playerClass, existingDeck.Cards.SelectMany(x => Enumerable.Repeat(x.Id, x.Count)));
				}
			}
		}

		public static void UpdateDungeonRunDeck(HearthSim.Core.Hearthstone.Deck dungeonDeck)
		{
			if(!Config.Instance.DungeonAutoImport || dungeonDeck == null)
				return;
			Log.Info("Found dungeon run deck!");
			var cards = dungeonDeck.Cards.ToList();
			if(!Config.Instance.DungeonRunIncludePassiveCards)
				cards.RemoveAll(c => !c.Data.Collectible && c.Data.Entity.GetTag(GameTag.HIDE_STATS) > 0);
			var deck = DeckList.Instance.Decks.FirstOrDefault(x => x.IsDungeonDeck && x.CardClass == dungeonDeck.Class
											&& !(x.IsDungeonRunCompleted ?? false)
											&& x.Cards.All(e => cards.Any(c => c.Id == e.Id && c.Count >= e.Count)));
			if(deck == null && (deck = CreateDungeonDeck(dungeonDeck)) == null)
			{
				Log.Info($"No existing deck - can't find default deck for {dungeonDeck.Class}");
				return;
			}
			if(cards.All(c => deck.Cards.Any(e => c.Id == e.Id && c.Count == e.Count)))
			{
				Log.Info("No new cards");
				return;
			}
			deck.Cards.Clear();
			Helper.SortCardCollection(cards, false);
			foreach(var card in cards)
				deck.Cards.Add(new Card(card.Data, card.Count));
			deck.LastEdited = DateTime.Now;
			DeckList.Save();
			Core.UpdatePlayerCards(true);
			Log.Info("Updated dungeon run deck");
		}

		private static Deck CreateDungeonDeck(HearthSim.Core.Hearthstone.Deck deck, CardSet set)
		{
			Log.Info($"Creating new {playerClass} dungeon run deck (CardSet={set})");
			var newDeck = ImportDeck(deck);
			newDeck.Name = Helper.ParseDeckNameTemplate(Config.Instance.DungeonRunDeckNameTemplate, newDeck);
			DeckList.Instance.Decks.Add(newDeck);
			DeckList.Save();
			return newDeck;
		}

		private static Deck TryFindDeck(HearthSim.Core.Hearthstone.Deck deck)
		{
			bool MatchesType(Deck d)
			{
				switch(deck.Type)
				{
					case DeckType.Constructed:
						return !d.IsArenaDeck && !d.IsBrawlDeck && !d.IsDungeonDeck;
					case DeckType.Arena:
						return d.IsArenaDeck;
					case DeckType.TavernBrawl:
						return d.IsBrawlDeck;
					case DeckType.DungeonRun:
						return d.IsDungeonDeck;
				}
				return false;
			}
			bool MatchesCards(Deck d) => deck.Cards.All(c1 => d.Cards.Any(c2 => c1.Id == c2.Id && c1.Count == c2.Count));
			bool MatchesClass(Deck d) => deck.Class == d.CardClass;
			bool Matches(Deck d) => MatchesType(d) && MatchesCards(d) && MatchesClass(d);
			var existing = DeckList.Instance.Decks.FirstOrDefault(Matches);
			return existing ?? DeckList.Instance.Decks.SelectMany(x => x.Versions).FirstOrDefault(Matches);
		}

		public static Deck ImportDeck(HearthSim.Core.Hearthstone.Deck deck)
		{
			var existing = TryFindDeck(deck);
			if(existing != null)
				return existing;
			var newDeck = new Deck
			{
				Cards = new ObservableCollection<Card>(deck.Cards.Select(x => new Card(x.Data, x.Count))),
				Class = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(deck.Class.ToString().ToLowerInvariant()),
				IsDungeonDeck = deck.Type == DeckType.DungeonRun,
				IsArenaDeck = deck.Type == DeckType.Arena,
				LastEdited = DateTime.Now,
				Name = deck.Name
			};
			if(deck.Type == DeckType.TavernBrawl)
				newDeck.Tags.Add("Brawl");

			DeckList.Instance.Decks.Add(newDeck);
			DeckList.Save();
			Core.MainWindow.DeckPickerList.UpdateDecks();
			Core.MainWindow.SelectDeck(newDeck, true);

			return newDeck;
		}

		public static void ImportDecks(IEnumerable<HearthSim.Core.Hearthstone.Deck> decks)
		{
			foreach(var deck in decks)
				ImportDeck(deck);
		}
	}

	public static class DeckListExtensions
	{
		public static List<Deck> FilterByMode(this List<Deck> decks, GameType mode, FormatType? format)
		{
			var filtered = new List<Deck>(decks);
			if(mode == GameType.GT_ARENA)
				filtered = filtered.Where(x => x.IsArenaDeck && x.IsArenaRunCompleted != true).ToList();
			else if(mode != GameType.GT_UNKNOWN)
			{
				filtered = filtered.Where(x => !x.IsArenaDeck).ToList();
				if(format == FormatType.FT_STANDARD)
					filtered = filtered.Where(x => x.StandardViable).ToList();
			}
			return filtered;
		}
	}
}
