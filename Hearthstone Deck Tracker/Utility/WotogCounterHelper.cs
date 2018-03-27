using System.Linq;
using System;
using HearthSim.Core.Hearthstone;
using HearthSim.Core.Hearthstone.Entities;
using static HearthDb.CardIds;
using static HearthDb.CardIds.Collectible;
using static HearthDb.Enums.GameTag;
using static Hearthstone_Deck_Tracker.Enums.DisplayMode;

namespace Hearthstone_Deck_Tracker.Utility
{
	public static class WotogCounterHelper
	{
		private static GameState Game => Core.Hearthstone.CurrentGame;
		private static bool IsInMenu => Core.Hearthstone.IsInMenu;
		private static Config Config => Config.Instance;

		public static Entity PlayerCthun =>
			Game?.LocalPlayer.Entities.FirstOrDefault(x => x.CardId == Neutral.Cthun && x.Info.OriginalZone != null);

		public static Entity PlayerCthunProxy =>
			Game?.LocalPlayer.Entities.FirstOrDefault(x => x.CardId == NonCollectible.Neutral.Cthun);

		public static Entity PlayerYogg =>
			Game?.LocalPlayer.Entities.FirstOrDefault(x => x.CardId == Neutral.YoggSaronHopesEnd && x.Info.OriginalZone != null);

		public static Entity PlayerArcaneGiant =>
			Game?.LocalPlayer.Entities.FirstOrDefault(x => x.CardId == Neutral.ArcaneGiant && x.Info.OriginalZone != null);

		public static Entity OpponentCthun => Game?.OpposingPlayer.Entities.FirstOrDefault(x => x.CardId == Neutral.Cthun);

		public static Entity OpponentCthunProxy =>
			Game?.OpposingPlayer.Entities.FirstOrDefault(x => x.CardId == NonCollectible.Neutral.Cthun);

		public static bool PlayerSeenCthun => Game?.LocalPlayerEntity?.HasTag(SEEN_CTHUN) ?? false;
		public static bool OpponentSeenCthun => Game?.OpposingPlayerEntity?.HasTag(SEEN_CTHUN) ?? false;
		public static bool? CthunInDeck => DeckContains(Neutral.Cthun);
		public static bool? YoggInDeck => DeckContains(Neutral.YoggSaronHopesEnd);
		public static bool? ArcaneGiantInDeck => DeckContains(Neutral.ArcaneGiant);

		public static bool PlayerSeenJade => Game?.LocalPlayerEntity?.HasTag(JADE_GOLEM) ?? false;

		public static int PlayerNextJadeGolem =>
			PlayerSeenJade ? Math.Min((Game?.LocalPlayerEntity.GetTag(JADE_GOLEM) ?? 0) + 1, 30) : 1;

		public static bool OpponentSeenJade => Game?.OpposingPlayerEntity?.HasTag(JADE_GOLEM) ?? false;

		public static int OpponentNextJadeGolem =>
			OpponentSeenJade ? Math.Min((Game?.OpposingPlayerEntity.GetTag(JADE_GOLEM) ?? 0) + 1, 30) : 1;

		public static bool ShowPlayerCthunCounter => !IsInMenu && (Config.PlayerCthunCounter == Always
					|| Config.PlayerCthunCounter == Auto && PlayerSeenCthun);

		public static bool ShowPlayerSpellsCounter => !IsInMenu && (
			Config.PlayerSpellsCounter == Always
				|| (Config.PlayerSpellsCounter == Auto && YoggInDeck.HasValue && (PlayerYogg != null || YoggInDeck.Value))
				|| (Config.PlayerSpellsCounter == Auto && ArcaneGiantInDeck.HasValue && (PlayerArcaneGiant != null || ArcaneGiantInDeck.Value))
			);

		public static bool ShowPlayerJadeCounter => !IsInMenu && (Config.PlayerJadeCounter == Always
					|| Config.PlayerJadeCounter == Auto && PlayerSeenJade);

		public static bool ShowOpponentCthunCounter => !IsInMenu && (Config.OpponentCthunCounter == Always
					|| Config.OpponentCthunCounter == Auto && OpponentSeenCthun);

		public static bool ShowOpponentSpellsCounter => !IsInMenu && Config.OpponentSpellsCounter == Always;

		public static bool ShowOpponentJadeCounter => !IsInMenu && (Config.OpponentJadeCounter == Always
					|| Config.OpponentJadeCounter == Auto && OpponentSeenJade);

		private static bool? DeckContains(string cardId) => DeckList.Instance.ActiveDeck?.Cards.Any(x => x.Id == cardId);

	}
}
