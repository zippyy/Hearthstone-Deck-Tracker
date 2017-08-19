using System;
using System.Linq;
using System.Threading.Tasks;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.LogReader.Interfaces;
using Hearthstone_Deck_Tracker.Utility.Logging;
using static HearthDb.Enums.GameTag;
using static HearthDb.Enums.PlayState;
using static HearthDb.Enums.Zone;

namespace Hearthstone_Deck_Tracker.LogReader.Handlers
{
	internal class TagChangeActions
	{
		public Action FindAction(GameTag tag, IGame game, ILogState state, int id, int value, int prevValue)
		{
			switch(tag)
			{
				case ZONE:
					return () => ZoneChange(state, id, game, value, prevValue);
				case PLAYSTATE:
					return () => PlaystateChange(state, id, game, value);
				case CARDTYPE:
					return () => CardTypeChange(state, id, game, value);
				case LAST_CARD_PLAYED:
					return () => LastCardPlayedChange(state, value);
				case DEFENDING:
					return () => DefendingChange(state, id, game, value);
				case ATTACKING:
					return () => AttackingChange(state, id, game, value);
				case NUM_MINIONS_PLAYED_THIS_TURN:
					return () => NumMinionsPlayedThisTurnChange(state, game, value);
				case PREDAMAGE:
					return () => PredamageChange(state, id, game, value);
				case NUM_TURNS_IN_PLAY:
					return () => NumTurnsInPlayChange(state, id, game, value);
				case CONTROLLER:
					return () => ControllerChange(state, id, game, prevValue, value);
				case FATIGUE:
					return () => FatigueChange(state, value, game, id);
				case STEP:
					return () => StepChange(state, game);
				case TURN:
					return () => TurnChange(state, game);
				case STATE:
					return () => StateChange(value, state);
				case TRANSFORMED_FROM_CARD:
					return () => TransformedFromCardChange(id, value, game);
			}
			return null;
		}

		private void TransformedFromCardChange(int id, int value, IGame game)
		{
			if(value == 0)
				return;
			if(game.Entities.TryGetValue(id, out var entity))
				entity.Info.SetOriginalCardId(value);
		}

		private void StateChange(int value, ILogState state)
		{
			if(value != (int)State.COMPLETE)
				return;
			state.GameHandler.HandleGameEnd();
			state.GameEnded = true;
		}

		private void TurnChange(ILogState state, IGame game)
		{
			if(!state.SetupDone || game.PlayerEntity == null)
				return;
			var activePlayer = game.PlayerEntity.HasTag(CURRENT_PLAYER) ? ActivePlayer.Player : ActivePlayer.Opponent;
			if(activePlayer == ActivePlayer.Player)
				state.PlayerUsedHeroPower = false;
			else
				state.OpponentUsedHeroPower = false;
		}

		private void StepChange(ILogState state, IGame game)
		{
			if(state.SetupDone || game.Entities.FirstOrDefault().Value?.Name != "GameEntity")
				return;
			Log.Info("Game was already in progress.");
			state.WasInProgress = true;
		}

		private void LastCardPlayedChange(ILogState state, int value) => state.LastCardPlayed = value;

		private void DefendingChange(ILogState state, int id, IGame game, int value)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			state.GameHandler.HandleDefendingEntity(value == 1 ? entity : null);
		}

		private void AttackingChange(ILogState state, int id, IGame game, int value)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			state.GameHandler.HandleAttackingEntity(value == 1 ? entity : null);
		}

		private void NumMinionsPlayedThisTurnChange(ILogState state, IGame game, int value)
		{
			if(value <= 0)
				return;
			if(game.PlayerEntity?.IsCurrentPlayer ?? false)
				state.GameHandler.HandlePlayerMinionPlayed();
		}

		private void PredamageChange(ILogState state, int id, IGame game, int value)
		{
			if(value <= 0)
				return;
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			state.GameHandler.HandleEntityPredamage(entity, value);
		}

		private void NumTurnsInPlayChange(ILogState state, int id, IGame game, int value)
		{
			if(value <= 0)
				return;
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			state.GameHandler.HandleTurnsInPlayChange(entity, state.GetTurnNumber());
		}

		private void FatigueChange(ILogState state, int value, IGame game, int id)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			var controller = entity.GetTag(CONTROLLER);
			if(controller == game.Player.Id)
				state.GameHandler.HandlePlayerFatigue(value);
			else if(controller == game.Opponent.Id)
				state.GameHandler.HandleOpponentFatigue(value);
		}

		private void ControllerChange(ILogState state, int id, IGame game, int prevValue, int value)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			if(prevValue <= 0)
			{
				entity.Info.OriginalController = value;
				return;
			}
			if(entity.HasTag(PLAYER_ID))
				return;
			if(value == game.Player.Id)
			{
				if(entity.IsInZone(Zone.SECRET))
					state.GameHandler.HandleOpponentStolen(entity, entity.CardId, state.GetTurnNumber());
				else if(entity.IsInZone(PLAY))
					state.GameHandler.HandleOpponentStolen(entity, entity.CardId, state.GetTurnNumber());
			}
			else if(value == game.Opponent.Id)
			{
				if(entity.IsInZone(Zone.SECRET))
					state.GameHandler.HandlePlayerStolen(entity, entity.CardId, state.GetTurnNumber());
				else if(entity.IsInZone(PLAY))
					state.GameHandler.HandlePlayerStolen(entity, entity.CardId, state.GetTurnNumber());
			}
		}

		private void CardTypeChange(ILogState state, int id, IGame game, int value)
		{
			if(value == (int)CardType.HERO)
				SetHeroAsync(id, game, state);
		}

		private void PlaystateChange(ILogState state, int id, IGame game, int value)
		{
			if(value == (int)CONCEDED)
				state.GameHandler.HandleConcede();
			if(state.GameEnded)
				return;
			if(!game.Entities.TryGetValue(id, out var entity) || !entity.IsPlayer)
				return;
			switch((PlayState)value)
			{
				case WON:
					state.GameHandler.HandleWin();
					break;
				case LOST:
					state.GameHandler.HandleLoss();
					break;
				case TIED:
					state.GameHandler.HandleTied();
					break;
			}
		}

		private void ZoneChange(ILogState state, int id, IGame game, int value, int prevValue)
		{
			if(id <= 3)
				return;
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			if(!entity.Info.OriginalZone.HasValue)
			{
				if(prevValue != (int)Zone.INVALID && prevValue != (int)SETASIDE)
					entity.Info.OriginalZone = (Zone)prevValue;
				else if(value != (int)Zone.INVALID && value != (int)SETASIDE)
					entity.Info.OriginalZone = (Zone)value;
			}
			var controller = entity.GetTag(CONTROLLER);
			switch((Zone)prevValue)
			{
				case DECK:
					ZoneChangeFromDeck(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				case HAND:
					ZoneChangeFromHand(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				case PLAY:
					ZoneChangeFromPlay(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				case Zone.SECRET:
					ZoneChangeFromSecret(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				case Zone.INVALID:
					var maxId = GetMaxHeroPowerId(game);
					if(!state.SetupDone && (id <= maxId || game.GameEntity?.GetTag(STEP) == (int)Step.INVALID && entity.GetTag(ZONE_POSITION) < 5))
					{
						entity.Info.OriginalZone = DECK;
						SimulateZoneChangesFromDeck(state, id, game, value, entity.CardId, maxId);
					}
					else
						ZoneChangeFromOther(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				case GRAVEYARD:
				case SETASIDE:
				case REMOVEDFROMGAME:
					ZoneChangeFromOther(state, id, game, value, prevValue, controller, entity.CardId);
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		// The last heropower is created after the last hero, therefore +1
		private int GetMaxHeroPowerId(IGame game) => 
			Math.Max(game.PlayerEntity?.GetTag(HERO_ENTITY) ?? 66, game.OpponentEntity?.GetTag(HERO_ENTITY) ?? 66) + 1;

		private void SimulateZoneChangesFromDeck(ILogState state, int id, IGame game, int value, string cardId, int maxId)
		{
			if(value == (int)DECK)
				return;
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			if(value == (int)SETASIDE)
			{
				entity.Info.Created = true;
				return;
			}
			if(entity.IsHero && !entity.IsPlayableHero || entity.IsHeroPower || entity.HasTag(PLAYER_ID) || entity.GetTag(CARDTYPE) == (int)CardType.GAME
				|| entity.HasTag(CREATOR))
				return;
			ZoneChangeFromDeck(state, id, game, (int)HAND, (int)DECK, entity.GetTag(CONTROLLER), cardId);
			if(value == (int)HAND)
				return;
			ZoneChangeFromHand(state, id, game, (int)PLAY, (int)HAND, entity.GetTag(CONTROLLER), cardId);
			if(value == (int)PLAY)
				return;
			ZoneChangeFromPlay(state, id, game, value, (int)PLAY, entity.GetTag(CONTROLLER), cardId);
		}

		private void ZoneChangeFromOther(ILogState state, int id, IGame game, int value, int prevValue, int controller, string cardId)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			if(entity.Info.OriginalZone == DECK && value != (int)DECK)
			{
				//This entity was moved from DECK to SETASIDE to HAND, e.g. by Tracking
				entity.Info.Discarded = false;
				ZoneChangeFromDeck(state, id, game, value, prevValue, controller, cardId);
				return;
			}
			entity.Info.Created = true;
			switch((Zone)value)
			{
				case PLAY:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerCreateInPlay(entity, cardId, state.GetTurnNumber());
					if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentCreateInPlay(entity, cardId, state.GetTurnNumber());
					break;
				case DECK:
					if(controller == game.Player.Id)
					{
						if(state.JoustReveals > 0)
							break;
						state.GameHandler.HandlePlayerGetToDeck(entity, cardId, state.GetTurnNumber());
					}
					if(controller == game.Opponent.Id)
					{
						if(state.JoustReveals > 0)
							break;
						state.GameHandler.HandleOpponentGetToDeck(entity, state.GetTurnNumber());
					}
					break;
				case HAND:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerGet(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentGet(entity, state.GetTurnNumber(), id);
					break;
				case Zone.SECRET:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerSecretPlayed(entity, cardId, state.GetTurnNumber(), (Zone)prevValue);
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentSecretPlayed(entity, cardId, -1, state.GetTurnNumber(), (Zone)prevValue, id);
					break;
				case SETASIDE:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerCreateInSetAside(entity, state.GetTurnNumber());
					if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentCreateInSetAside(entity, state.GetTurnNumber());
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		private void ZoneChangeFromSecret(ILogState state, int id, IGame game, int value, int prevValue, int controller, string cardId)
		{
			switch((Zone)value)
			{
				case Zone.SECRET:
				case GRAVEYARD:
					if(controller == game.Opponent.Id)
					{
						if(!game.Entities.TryGetValue(id, out var entity))
							return;
						state.GameHandler.HandleOpponentSecretTrigger(entity, cardId, state.GetTurnNumber(), id);
					}
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		private void ZoneChangeFromPlay(ILogState state, int id, IGame game, int value, int prevValue, int controller, string cardId)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			switch((Zone)value)
			{
				case HAND:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerBackToHand(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentPlayToHand(entity, cardId, state.GetTurnNumber(), id);
					break;
				case DECK:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerPlayToDeck(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentPlayToDeck(entity, cardId, state.GetTurnNumber());
					break;
				case GRAVEYARD:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerPlayToGraveyard(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentPlayToGraveyard(entity, cardId, state.GetTurnNumber(), game.PlayerEntity?.IsCurrentPlayer ?? false);
					break;
				case REMOVEDFROMGAME:
				case SETASIDE:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerRemoveFromPlay(entity, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentRemoveFromPlay(entity, state.GetTurnNumber());
					break;
				case PLAY:
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		private void ZoneChangeFromHand(ILogState state, int id, IGame game, int value, int prevValue, int controller, string cardId)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			switch((Zone)value)
			{
				case PLAY:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerPlay(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
					{
						state.GameHandler.HandleOpponentPlay(entity, cardId, entity.GetTag(ZONE_POSITION),
																 state.GetTurnNumber());
					}
					break;
				case REMOVEDFROMGAME:
				case SETASIDE:
				case GRAVEYARD:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerHandDiscard(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
					{
						state.GameHandler.HandleOpponentHandDiscard(entity, cardId, entity.GetTag(ZONE_POSITION),
																		state.GetTurnNumber());
					}
					break;
				case Zone.SECRET:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerSecretPlayed(entity, cardId, state.GetTurnNumber(), (Zone)prevValue);
					else if(controller == game.Opponent.Id)
					{
						state.GameHandler.HandleOpponentSecretPlayed(entity, cardId, entity.GetTag(ZONE_POSITION),
																		 state.GetTurnNumber(), (Zone)prevValue, id);
					}
					break;
				case DECK:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerMulligan(entity, cardId);
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentMulligan(entity, entity.GetTag(ZONE_POSITION));
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		private void ZoneChangeFromDeck(ILogState state, int id, IGame game, int value, int prevValue, int controller, string cardId)
		{
			if(!game.Entities.TryGetValue(id, out var entity))
				return;
			switch((Zone)value)
			{
				case HAND:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerDraw(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentDraw(entity, state.GetTurnNumber());
					break;
				case SETASIDE:
				case REMOVEDFROMGAME:
					if(!state.SetupDone)
					{
						entity.Info.Created = true;
						return;
					}
					if(controller == game.Player.Id)
					{
						if(state.JoustReveals > 0)
						{
							state.JoustReveals--;
							break;
						}
						state.GameHandler.HandlePlayerRemoveFromDeck(entity, state.GetTurnNumber());
					}
					else if(controller == game.Opponent.Id)
					{
						if(state.JoustReveals > 0)
						{
							state.JoustReveals--;
							break;
						}
						state.GameHandler.HandleOpponentRemoveFromDeck(entity, state.GetTurnNumber());
					}
					break;
				case GRAVEYARD:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerDeckDiscard(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentDeckDiscard(entity, cardId, state.GetTurnNumber());
					break;
				case PLAY:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerDeckToPlay(entity, cardId, state.GetTurnNumber());
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentDeckToPlay(entity, cardId, state.GetTurnNumber());
					break;
				case Zone.SECRET:
					if(controller == game.Player.Id)
						state.GameHandler.HandlePlayerSecretPlayed(entity, cardId, state.GetTurnNumber(), (Zone)prevValue);
					else if(controller == game.Opponent.Id)
						state.GameHandler.HandleOpponentSecretPlayed(entity, cardId, -1, state.GetTurnNumber(), (Zone)prevValue, id);
					break;
				default:
					Log.Warn($"unhandled zone change (id={id}): {prevValue} -> {value}");
					break;
			}
		}

		private async void SetHeroAsync(int id, IGame game, ILogState state)
		{
			Log.Info("Found hero with id=" + id);
			if(game.PlayerEntity == null)
			{
				Log.Info("Waiting for PlayerEntity to exist");
				while(game.PlayerEntity == null)
					await Task.Delay(100);
				Log.Info("Found PlayerEntity");
			}
			if(string.IsNullOrEmpty(game.Player.Class) && id == game.PlayerEntity.GetTag(HERO_ENTITY))
			{
				if(!game.Entities.TryGetValue(id, out var entity))
					return;
				state.GameHandler.SetPlayerHero(Database.GetHeroNameFromId(entity.CardId));
				return;
			}
			if(game.OpponentEntity == null)
			{
				Log.Info("Waiting for OpponentEntity to exist");
				while(game.OpponentEntity == null)
					await Task.Delay(100);
				Log.Info("Found OpponentEntity");
			}
			if(string.IsNullOrEmpty(game.Opponent.Class) && id == game.OpponentEntity.GetTag(HERO_ENTITY))
			{
				if(!game.Entities.TryGetValue(id, out var entity))
					return;
				state.GameHandler.SetOpponentHero(Database.GetHeroNameFromId(entity.CardId));
			}
		}
	}
}
