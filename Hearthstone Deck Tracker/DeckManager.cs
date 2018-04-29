using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay.Utility;
using Hearthstone_Deck_Tracker.Importing.Game;
using Hearthstone_Deck_Tracker.Importing.Game.ImportOptions;
using Hearthstone_Deck_Tracker.Replay;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility.Logging;
using HearthSim.Core.Hearthstone;
using HearthSim.Core.Hearthstone.Entities;
using HearthSim.Core.Util.EventArgs;
using HSReplay;
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
				var revealed = RevealedEntites;
				var localHero = Core.Hearthstone.CurrentGame?.LocalPlayer?.CurrentHero;
				var playerClass = localHero?.Class ?? CardClass.INVALID;
				var existingDeck = DeckList.Instance.Decks
					.Where(x => x.IsDungeonDeck && playerClass == x.CardClass && !(x.IsDungeonRunCompleted ?? false)
								&& (!newRun || x.Cards.Count == 10) && GetMissingCards(revealed, x).Count == 0)
					.OrderByDescending(x => x.LastEdited).FirstOrDefault();
				if(existingDeck == null)
				{
					Log.Info("We don't have an existing deck for this run");
					var set = (CardSet)(localHero?.GetTag(GameTag.CARD_SET) ?? 0);
					CreateDungeonDeck(playerClass, set);
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

		private static Deck CreateDungeonDeck(CardClass cardClass, CardSet cardSet)
		{
			return CreateDungeonDeck(DungeonRun.GetDefaultDeck(cardClass, cardSet));
		}
		private static Deck CreateDungeonDeck(HearthSim.Core.Hearthstone.Deck deck)
		{
			if(deck == null)
				return null;
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
				Class = ToTitleCase(deck.Class.ToString()),
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

		public static GameStats HandleMatchResults(GameEndEventArgs args)
		{
			var playedDeck = args.GameState.LocalPlayer.Deck;
			if(playedDeck == null)
			{
				//Todo
				return null;
			}
			var deck = ImportDeck(playedDeck);

			var playState = (PlayState)args.GameState.LocalPlayerEntity.GetTag(GameTag.PLAYSTATE);
			var gameResult = GetGameResult(playState);

			var localHero = args.GameState.LocalPlayer.Entities.FirstOrDefault(x => x.IsHero);
			var opposingHero = args.GameState.OpposingPlayer.Entities.FirstOrDefault(x => x.IsHero);

			if(localHero == null || opposingHero == null)
			{
				//Todo
				return null;
			}

			List<TrackedCard> RevealedCards(HearthSim.Core.Hearthstone.Player player)
			{
				return player.RevealedCards.Where(x => !x.IsCreated && !x.Info.Stolen).GroupBy(x => x.Info.OriginalCardId)
					.Select(x => new TrackedCard(x.Key, x.Count())).ToList();
			}

			var matchInfo = args.GameState.MatchInfo;
			var gameType = (GameType)matchInfo.GameType;
			var standard = (FormatType)matchInfo.FormatType == FormatType.FT_STANDARD;
			var format = standard ? Format.Standard : Format.Wild;
			var gameMode = HearthDbConverter.GetGameMode(gameType);
			var game = new GameStats(gameResult, ToTitleCase(opposingHero.Class.ToString()),
				ToTitleCase(localHero.Class.ToString()))
			{
				GameType = gameType,
				ArenaWins = gameType == GameType.GT_ARENA ? args.Wins : 0,
				ArenaLosses = gameType == GameType.GT_ARENA ? args.Losses : 0,
				BrawlWins = gameMode == GameMode.Brawl ? args.Wins : 0,
				BrawlLosses = gameMode == GameMode.Brawl ? args.Losses : 0,
				BrawlSeasonId = gameMode == GameMode.Brawl ? args.GameState.MatchInfo.BrawlSeasonId : 0,
				Coin = args.GameState.OpposingPlayerEntity.HasTag(GameTag.FIRST_PLAYER),
				DeckId = deck.DeckId,
				DeckName = deck.Name,
				EndTime = args.GameState.GameTime.Time,
				Format = format,
				FriendlyPlayerId = args.GameState.LocalPlayer.PlayerId,
				GameMode = gameMode,
				HearthstoneBuild = args.Build,
				HsDeckId = args.GameState.LocalPlayer.Deck.DeckId,
				HsReplay = new HsReplayInfo(),
				LegendRank = standard ? matchInfo.LocalPlayer.StandardLegendRank : matchInfo.LocalPlayer.WildLegendRank,
				OpponentCardbackId = matchInfo.OpposingPlayer.CardBackId,
				OpponentCards = RevealedCards(args.GameState.OpposingPlayer),
				OpponentHeroCardId = args.GameState.OpposingPlayer.Entities.FirstOrDefault(x => x.IsHero)?.CardId,
				OpponentLegendRank = standard ? matchInfo.OpposingPlayer.StandardLegendRank : matchInfo.OpposingPlayer.WildLegendRank,
				OpponentName = matchInfo.OpposingPlayer.Name,
				OpponentRank = standard ? matchInfo.OpposingPlayer.StandardRank : matchInfo.OpposingPlayer.WildRank,
				PlayerCardbackId = matchInfo.LocalPlayer.CardBackId,
				PlayerCards = RevealedCards(args.GameState.OpposingPlayer),
				PlayerDeckVersion = deck.Version,
				PlayerName = matchInfo.LocalPlayer.Name,
				Rank = standard ? matchInfo.LocalPlayer.StandardRank : matchInfo.LocalPlayer.WildRank,
				RankedSeasonId = matchInfo.RankedSeasonId,
				Region = (Region)args.Region,
				ScenarioId = matchInfo.MissionId,
				ServerInfo = args.GameState.ServerInfo,
				Stars = standard ? matchInfo.LocalPlayer.StandardStars : matchInfo.LocalPlayer.WildStars,
				StartTime = args.GameState.CreatedAt,
				Turns = args.GameState.CurrentTurn,
				WasConceded = args.GameState.LocalPlayerEntity.Conceded,
			};

			game.ReplayFile = ReplayMaker.SaveToDisk(game, args.GameState.PowerLog.ToList());
			LastGame = game;

			deck.DeckStats.AddGameResult(game);
			DeckStatsList.Save();
			Core.MainWindow.DeckPickerList.UpdateDecks(false, new[] { deck });
			return game;
		}

		public static GameStats LastGame { get; set; }

		private static GameResult GetGameResult(PlayState playState)
		{
			switch(playState)
			{
				case PlayState.WON:
					return GameResult.Win;
				case PlayState.LOST:
					return GameResult.Loss;
				case PlayState.TIED:
					return GameResult.Draw;
			}
			return GameResult.None;
		}

		private static string ToTitleCase(string str)
		{
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(str.ToLowerInvariant());
		}

		public static void SetUploadStatus(UploadCompleteEventArgs args)
		{
			if(IsLastGame(args.Data))
			{
				LastGame.HsReplay.UploadId = args.Status.ShortId;
				LastGame.HsReplay.ReplayUrl = args.Status.ReplayUrl;
				//TODO: consider deleting replay file
			}
		}

		public static void SetUploadStatus(UploadErrorEventArgs args)
		{
			if(IsLastGame(args.Data))
				LastGame.HsReplay.Unsupported = true;
		}

		private static bool IsLastGame(UploadMetaData data)
		{
			return LastGame != null && data.GameHandle == LastGame.ServerInfo?.GameHandle.ToString()
									&& data.AuroraPassword == LastGame.ServerInfo?.AuroraPassword;
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
