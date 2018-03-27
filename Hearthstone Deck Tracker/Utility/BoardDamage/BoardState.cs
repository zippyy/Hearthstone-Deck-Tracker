using System.Collections.Generic;
using System.Linq;
using HearthSim.Core.Hearthstone.Entities;

namespace Hearthstone_Deck_Tracker.Utility.BoardDamage
{
	public class BoardState
	{
		public BoardState()
		{
			Player = CreatePlayerBoard();
			Opponent = CreateOpponentBoard();
		}

		public BoardState(List<Entity> player, List<Entity> opponent, Dictionary<int, Entity> entities, int playerId)
		{
			Player = CreateBoard(player, entities, true, playerId);
			Opponent = CreateBoard(opponent, entities, false, playerId);
		}

		public PlayerBoard Player { get; }
		public PlayerBoard Opponent { get; }

		public bool IsPlayerDeadToBoard() => Player.Hero == null || Opponent.Damage >= Player.Hero.Health;

		public bool IsOpponentDeadToBoard() => Opponent.Hero == null || Player.Damage >= Opponent.Hero.Health;

		private PlayerBoard CreatePlayerBoard() =>
			CreateBoard(new List<Entity>(Core.Hearthstone.CurrentGame.LocalPlayer.InPlay),
				Core.Hearthstone.CurrentGame.Entities, true, Core.Hearthstone.CurrentGame.LocalPlayer.PlayerId);

		private PlayerBoard CreateOpponentBoard() =>
			CreateBoard(new List<Entity>(Core.Hearthstone.CurrentGame.OpposingPlayer.InPlay),
				Core.Hearthstone.CurrentGame.Entities, false, Core.Hearthstone.CurrentGame.OpposingPlayer.PlayerId);

		private PlayerBoard CreateBoard(List<Entity> list, Dictionary<int, Entity> entities, bool isPlayer, int playerId)
		{
			var activeTurn = !(EntityHelper.IsPlayersTurn(entities) ^ isPlayer);
			// if there is no hero in the list, try to find it
			var heroFound = list.Any(EntityHelper.IsHero);
			if(!heroFound)
				list?.Add(EntityHelper.GetHeroEntity(isPlayer, entities, playerId));

			return new PlayerBoard(list, activeTurn);
		}
	}
}
