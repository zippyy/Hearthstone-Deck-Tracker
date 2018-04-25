using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;
using static HearthDb.Enums.BnetGameType;
using static Hearthstone_Deck_Tracker.Enums.GameMode;

namespace Hearthstone_Deck_Tracker.Utility
{
	public class GameTypeHelper
	{
		private static readonly Dictionary<GameMode, List<BnetGameType>> Dict = new Dictionary<GameMode, List<BnetGameType>>
		{
			[Ranked] = new List<BnetGameType>
			{
				BGT_RANKED_STANDARD,
				BGT_RANKED_WILD
			},
			[Casual] = new List<BnetGameType>
			{
				BGT_CASUAL_STANDARD,
				BGT_CASUAL_STANDARD_NEWBIE,
				BGT_CASUAL_STANDARD_NORMAL,
				BGT_CASUAL_WILD
			},
			[Arena] = new List<BnetGameType>
			{
				BGT_ARENA
			},
			[Brawl] = new List<BnetGameType>
			{
				BGT_TAVERNBRAWL_1P_VERSUS_AI,
				BGT_TAVERNBRAWL_2P_COOP,
				BGT_TAVERNBRAWL_PVP,
				BGT_FSG_BRAWL_1P_VERSUS_AI,
				BGT_FSG_BRAWL_2P_COOP,
				BGT_FSG_BRAWL_PVP,
				BGT_FSG_BRAWL_VS_FRIEND
			},
			[Practice] = new List<BnetGameType>
			{
				BGT_VS_AI
			},
			[Friendly] = new List<BnetGameType>
			{
				BGT_FRIENDS
			}
		};

		public static IEnumerable<BnetGameType> All =>
			Enum.GetValues(typeof(BnetGameType)).OfType<BnetGameType>();

		public static IEnumerable<BnetGameType> GetFromConfig()
		{
			return GetGameModes().SelectMany(x => Dict[x]);
		}

		private static IEnumerable<GameMode> GetGameModes()
		{
			if(Config.Instance.HsReplayUploadArena)
				yield return Arena;
			if(Config.Instance.HsReplayUploadBrawl)
				yield return Brawl;
			if(Config.Instance.HsReplayUploadCasual)
				yield return Casual;
			if(Config.Instance.HsReplayUploadFriendly)
				yield return Friendly;
			if(Config.Instance.HsReplayUploadPractice)
				yield return Practice;
			if(Config.Instance.HsReplayUploadRanked)
				yield return Ranked;
		}
	}

}
