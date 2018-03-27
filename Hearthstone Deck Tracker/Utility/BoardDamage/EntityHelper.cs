using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using HearthSim.Core.Hearthstone.Entities;
using static HearthDb.Enums.GameTag;

namespace Hearthstone_Deck_Tracker.Utility.BoardDamage
{
	public static class EntityHelper
	{
		public static bool IsHero(Entity e) => e.HasTag(CARDTYPE) && e.GetTag(CARDTYPE) == (int)CardType.HERO && e.HasTag(ZONE)
											   && e.GetTag(ZONE) == (int)Zone.PLAY;

		public static Entity GetHeroEntity(bool forPlayer)
		{
			return GetHeroEntity(forPlayer, Core.Hearthstone.CurrentGame.Entities,
				Core.Hearthstone.CurrentGame.LocalPlayer.PlayerId);
		}

		public static Entity GetHeroEntity(bool forPlayer, Dictionary<int, Entity> entities, int id)
		{
			if(!forPlayer)
				id = (id % 2) + 1;
			var heroes = entities.Where(x => IsHero(x.Value)).Select(x => x.Value).ToList();
			return heroes.FirstOrDefault(x => x.GetTag(CONTROLLER) == id);
		}

		public static bool IsPlayersTurn() => IsPlayersTurn(Core.Hearthstone.CurrentGame.Entities);

		public static bool IsPlayersTurn(Dictionary<int, Entity> entities)
		{
			if(entities.FirstOrDefault(e => e.Value.HasTag(FIRST_PLAYER)).Value is PlayerEntity firstPlayer)
			{
				if(!(entities.FirstOrDefault(x => x.Value is GameEntity).Value is GameEntity gameEntity))
					return false;
				var turn = gameEntity.GetTag(TURN);
				var isLocalPlayer = Core.Hearthstone.CurrentGame.LocalPlayer.PlayerId == firstPlayer.PlayerId;
				var offset = isLocalPlayer ? 0 : 1;
				return turn > 0 && ((turn + offset)%2 == 1);
			}
			return false;
		}
	}
}
