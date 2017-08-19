#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HearthDb.Enums;
using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.LogReader.Interfaces;
using Hearthstone_Deck_Tracker.Utility.Logging;
using static HearthDb.CardIds;
using static Hearthstone_Deck_Tracker.LogReader.LogConstants.PowerTaskList;

#endregion

namespace Hearthstone_Deck_Tracker.LogReader.Handlers
{
	public class PowerHandler
	{
		public void Handle(string logLine, ILogState state, IGame game)
		{
			var creationTag = false;
			if(GameEntityRegex.IsMatch(logLine))
			{
				var match = GameEntityRegex.Match(logLine);
				var id = int.Parse(match.Groups["id"].Value);
				if(!game.Entities.ContainsKey(id))
					game.Entities.Add(id, new Entity(id) {Name = "GameEntity"});
				state.SetCurrentEntity(id);
				if(state.DeterminedPlayers)
					state.TagChangeHandler.InvokeQueuedActions(game);
				return;
			}
			if(PlayerEntityRegex.IsMatch(logLine))
			{
				var match = PlayerEntityRegex.Match(logLine);
				var id = int.Parse(match.Groups["id"].Value);
				if(!game.Entities.ContainsKey(id))
					game.Entities.Add(id, new Entity(id));
				if(state.WasInProgress)
					game.Entities[id].Name = game.GetStoredPlayerName(id);
				state.SetCurrentEntity(id);
				if(state.DeterminedPlayers)
					state.TagChangeHandler.InvokeQueuedActions(game);
				game.AccountIds.Add(new AccountId
				{
					Hi = ulong.Parse(match.Groups["hi"].Value),
					Lo = ulong.Parse(match.Groups["lo"].Value)
				});
				return;
			}
			if(TagChangeRegex.IsMatch(logLine))
			{
				var match = TagChangeRegex.Match(logLine);
				var rawEntity = match.Groups["entity"].Value.Replace("UNKNOWN ENTITY ", "");
				if(rawEntity.StartsWith("[") && EntityRegex.IsMatch(rawEntity))
				{
					var entity = EntityRegex.Match(rawEntity);
					var id = int.Parse(entity.Groups["id"].Value);
					state.TagChangeHandler.TagChange(state, match.Groups["tag"].Value, id, match.Groups["value"].Value, game);
				}
				else if(int.TryParse(rawEntity, out int entityId))
					state.TagChangeHandler.TagChange(state, match.Groups["tag"].Value, entityId, match.Groups["value"].Value, game);
				else
				{
					var entity = game.Entities.FirstOrDefault(x => x.Value.Name == rawEntity);
					if (entity.Value != null)
						state.TagChangeHandler.TagChange(state, match.Groups["tag"].Value, entity.Key, match.Groups["value"].Value, game);
					else
					{
						var players = game.Entities.Where(x => x.Value.HasTag(GameTag.PLAYER_ID)).Take(2).ToList();
						var unnamedPlayers = players.Where(x => string.IsNullOrEmpty(x.Value.Name)).ToList();
						var unknownHumanPlayer = players.FirstOrDefault(x => x.Value.Name == "UNKNOWN HUMAN PLAYER");
						if(unnamedPlayers.Count == 0 && unknownHumanPlayer.Value != null)
						{
							Log.Info("Updating UNKNOWN HUMAN PLAYER");
							entity = unknownHumanPlayer;
						}

						//while the id is unknown, store in tmp entities
						var tmpEntity = state.TmpEntities.FirstOrDefault(x => x.Name == rawEntity);
						if(tmpEntity == null)
						{
							tmpEntity = new Entity(state.TmpEntities.Count + 1) { Name = rawEntity };
							state.TmpEntities.Add(tmpEntity);
						}
						Enum.TryParse(match.Groups["tag"].Value, out GameTag tag);
						var value = GameTagHelper.ParseTag(tag, match.Groups["value"].Value);
						if(unnamedPlayers.Count == 1)
							entity = unnamedPlayers.Single();
						else if(unnamedPlayers.Count == 2 && tag == GameTag.CURRENT_PLAYER && value == 0)
							entity = game.Entities.FirstOrDefault(x => x.Value?.HasTag(GameTag.CURRENT_PLAYER) ?? false);

						if(entity.Value != null)
						{
							tmpEntity.SetTag(tag, value);
							state.TransferTempData(tmpEntity, entity.Value);
						}
						if(state.TmpEntities.Contains(tmpEntity))
						{
							tmpEntity.SetTag(tag, value);
							var player = game.Player.Name == tmpEntity.Name ? game.Player : (game.Opponent.Name == tmpEntity.Name ? game.Opponent : null);
							if(player != null)
							{
								var playerEntity = game.Entities.FirstOrDefault(x => x.Value.GetTag(GameTag.PLAYER_ID) == player.Id).Value;
								if(playerEntity != null)
									state.TransferTempData(tmpEntity, playerEntity);
							}
						}
					}
				}
			}
			else if(CreationRegex.IsMatch(logLine))
			{
				var match = CreationRegex.Match(logLine);
				var id = int.Parse(match.Groups["id"].Value);
				var cardId = match.Groups["cardId"].Value;
				var zone = GameTagHelper.ParseEnum<Zone>(match.Groups["zone"].Value);
				if(!game.Entities.ContainsKey(id))
				{
					if(string.IsNullOrEmpty(cardId) && zone != Zone.SETASIDE)
					{
						var blockId = state.CurrentBlock?.Id;
						if(blockId.HasValue && state.KnownCardIds.ContainsKey(blockId.Value))
						{
							cardId = state.KnownCardIds[blockId.Value].FirstOrDefault();
							if(!string.IsNullOrEmpty(cardId))
							{
								Log.Info($"Found known cardId for entity {id}: {cardId}");
								state.KnownCardIds[blockId.Value].Remove(cardId);
							}
						}
					}
					game.Entities.Add(id, new Entity(id) {CardId = cardId});
				}
				state.SetCurrentEntity(id);
				if(state.DeterminedPlayers)
					state.TagChangeHandler.InvokeQueuedActions(game);
				state.CurrentEntityHasCardId = !string.IsNullOrEmpty(cardId);
				state.CurrentEntityZone = zone;
				return;
			}
			else if(UpdatingEntityRegex.IsMatch(logLine))
			{
				var match = UpdatingEntityRegex.Match(logLine);
				var cardId = match.Groups["cardId"].Value;
				var rawEntity = match.Groups["entity"].Value;
				int entityId;
				if(rawEntity.StartsWith("[") && EntityRegex.IsMatch(rawEntity))
				{
					var entity = EntityRegex.Match(rawEntity);
					entityId = int.Parse(entity.Groups["id"].Value);
				}
				else if(!int.TryParse(rawEntity, out entityId))
					entityId = -1;
				if(entityId != -1)
				{
					if(!game.Entities.ContainsKey(entityId))
						game.Entities.Add(entityId, new Entity(entityId));
					game.Entities[entityId].CardId = cardId;
					state.SetCurrentEntity(entityId);
					if(state.DeterminedPlayers)
						state.TagChangeHandler.InvokeQueuedActions(game);
				}
				if(state.JoustReveals > 0)
				{
					if(game.Entities.TryGetValue(entityId, out Entity currentEntity))
					{
						if(currentEntity.IsControlledBy(game.Opponent.Id))
							state.GameHandler.HandleOpponentJoust(currentEntity, cardId, state.GetTurnNumber());
						else if(currentEntity.IsControlledBy(game.Player.Id))
							state.GameHandler.HandlePlayerJoust(currentEntity, cardId, state.GetTurnNumber());
					}
				}
				return;
			}
			else if(CreationTagRegex.IsMatch(logLine) && !logLine.Contains("HIDE_ENTITY"))
			{
				var match = CreationTagRegex.Match(logLine);
				state.TagChangeHandler.TagChange(state, match.Groups["tag"].Value, state.CurrentEntityId, match.Groups["value"].Value, game, true);
				creationTag = true;
			}
			if(logLine.Contains("End Spectator"))
				state.GameHandler.HandleGameEnd();
			else if(logLine.Contains("BLOCK_START"))
			{
				var match = BlockStartRegex.Match(logLine);
				var blockType = match.Success ? match.Groups["type"].Value : null;
				state.BlockStart(blockType);

				if(match.Success && (blockType == "TRIGGER" || blockType == "POWER"))
				{
					var playerEntity =
						game.Entities.FirstOrDefault(
							e => e.Value.HasTag(GameTag.PLAYER_ID) && e.Value.GetTag(GameTag.PLAYER_ID) == game.Player.Id);
					var opponentEntity =
						game.Entities.FirstOrDefault(
							e => e.Value.HasTag(GameTag.PLAYER_ID) && e.Value.GetTag(GameTag.PLAYER_ID) == game.Opponent.Id);

					var actionStartingCardId = match.Groups["cardId"].Value.Trim();
					var actionStartingEntityId = int.Parse(match.Groups["id"].Value);

					if(string.IsNullOrEmpty(actionStartingCardId))
					{
						if(game.Entities.TryGetValue(actionStartingEntityId, out Entity actionEntity))
							actionStartingCardId = actionEntity.CardId;
					}
					if(string.IsNullOrEmpty(actionStartingCardId))
						return;
					if(blockType == "TRIGGER")
					{
						switch(actionStartingCardId)
						{
							case Collectible.Rogue.TradePrinceGallywix:
								AddKnownCardId(state, game.Entities[state.LastCardPlayed].CardId);
								AddKnownCardId(state, NonCollectible.Neutral.TradePrinceGallywix_GallywixsCoinToken);
								break;
							case Collectible.Shaman.WhiteEyes:
								AddKnownCardId(state, NonCollectible.Shaman.WhiteEyes_TheStormGuardianToken);
								break;
							case Collectible.Hunter.RaptorHatchling:
								AddKnownCardId(state, NonCollectible.Hunter.RaptorHatchling_RaptorPatriarchToken);
								break;
							case Collectible.Warrior.DirehornHatchling:
								AddKnownCardId(state, NonCollectible.Warrior.DirehornHatchling_DirehornMatriarchToken);
								break;
							case Collectible.Mage.FrozenClone:
								AddKnownCardId(state, GetTargetCardId(match), 2);
								break;
							case Collectible.Shaman.Moorabi:
								AddKnownCardId(state, GetTargetCardId(match));
								break;
						}
					}
					else //POWER
					{
						switch(actionStartingCardId)
						{
							case Collectible.Rogue.GangUp:
								AddKnownCardId(state, GetTargetCardId(match), 3);
								break;
							case Collectible.Rogue.BeneathTheGrounds:
								AddKnownCardId(state, NonCollectible.Rogue.BeneaththeGrounds_AmbushToken, 3);
								break;
							case Collectible.Warrior.IronJuggernaut:
								AddKnownCardId(state, NonCollectible.Warrior.IronJuggernaut_BurrowingMineToken);
								break;
							case Collectible.Druid.Recycle:
							case Collectible.Mage.ManicSoulcaster:
								AddKnownCardId(state, GetTargetCardId(match));
								break;
							case Collectible.Mage.ForgottenTorch:
								AddKnownCardId(state, NonCollectible.Mage.ForgottenTorch_RoaringTorchToken);
								break;
							case Collectible.Warlock.CurseOfRafaam:
								AddKnownCardId(state, NonCollectible.Warlock.CurseofRafaam_CursedToken);
								break;
							case Collectible.Neutral.AncientShade:
								AddKnownCardId(state, NonCollectible.Neutral.AncientShade_AncientCurseToken);
								break;
							case Collectible.Priest.ExcavatedEvil:
								AddKnownCardId(state, Collectible.Priest.ExcavatedEvil);
								break;
							case Collectible.Neutral.EliseStarseeker:
								AddKnownCardId(state, NonCollectible.Neutral.EliseStarseeker_MapToTheGoldenMonkeyToken);
								break;
							case NonCollectible.Neutral.EliseStarseeker_MapToTheGoldenMonkeyToken:
								AddKnownCardId(state, NonCollectible.Neutral.EliseStarseeker_GoldenMonkeyToken);
								break;
							case Collectible.Neutral.Doomcaller:
								AddKnownCardId(state, NonCollectible.Neutral.Cthun);
								break;
							case Collectible.Druid.JadeIdol:
								AddKnownCardId(state, Collectible.Druid.JadeIdol, 3);
								break;
							case NonCollectible.Hunter.TheMarshQueen_QueenCarnassaToken:
								AddKnownCardId(state, NonCollectible.Hunter.TheMarshQueen_CarnassasBroodToken, 15);
								break;
							case Collectible.Neutral.EliseTheTrailblazer:
								AddKnownCardId(state, NonCollectible.Neutral.ElisetheTrailblazer_UngoroPackToken);
								break;
							case Collectible.Mage.GhastlyConjurer:
								AddKnownCardId(state, Collectible.Mage.MirrorImage);
								break;
							default:
								if(playerEntity.Value != null && playerEntity.Value.GetTag(GameTag.CURRENT_PLAYER) == 1
									&& !state.PlayerUsedHeroPower
									|| opponentEntity.Value != null && opponentEntity.Value.GetTag(GameTag.CURRENT_PLAYER) == 1
									&& !state.OpponentUsedHeroPower)
								{
									var card = Database.GetCardFromId(actionStartingCardId);
									if(card.Type == "Hero Power")
									{
										if(playerEntity.Value != null && playerEntity.Value.GetTag(GameTag.CURRENT_PLAYER) == 1)
										{
											state.GameHandler.HandlePlayerHeroPower(actionStartingCardId, state.GetTurnNumber());
											state.PlayerUsedHeroPower = true;
										}
										else if(opponentEntity.Value != null)
										{
											state.GameHandler.HandleOpponentHeroPower(actionStartingCardId, state.GetTurnNumber());
											state.OpponentUsedHeroPower = true;
										}
									}
								}
								break;
						}
					}
				}
				else if(logLine.Contains("BlockType=JOUST"))
					state.JoustReveals = 2;
				else if(state.GameTriggerCount == 0 && logLine.Contains("BLOCK_START BlockType=TRIGGER Entity=GameEntity"))
					state.GameTriggerCount++;
			}
			else if(logLine.Contains("CREATE_GAME"))
				state.TagChangeHandler.ClearQueuedActions();
			else if(logLine.Contains("BLOCK_END"))
			{
				if(state.GameTriggerCount < 10 && (game.GameEntity?.HasTag(GameTag.TURN) ?? false))
				{
					state.GameTriggerCount += 10;
					state.TagChangeHandler.InvokeQueuedActions(game);
					state.SetupDone = true;
				}
				if(state.CurrentBlock?.Type == "JOUST")
				{
					//make sure there are no more queued actions that might depend on JoustReveals
					state.TagChangeHandler.InvokeQueuedActions(game);
					state.JoustReveals = 0;
				}

				state.BlockEnd();
			}


			if(game.IsInMenu)
				return;
			if(!creationTag && state.DeterminedPlayers)
				state.TagChangeHandler.InvokeQueuedActions(game);
			if(!creationTag)
				state.ResetCurrentEntity();
		}

		private static string GetTargetCardId(Match match)
		{
			var target = match.Groups["target"].Value.Trim();
			if(!target.StartsWith("[") || !EntityRegex.IsMatch(target))
				return null;
			var cardIdMatch = CardIdRegex.Match(target);
			return !cardIdMatch.Success ? null : cardIdMatch.Groups["cardId"].Value.Trim();
		}

		private static void AddKnownCardId(ILogState state, string cardId, int count = 1)
		{
			var blockId = state.CurrentBlock.Id;
			for(var i = 0; i < count; i++)
			{
				if(!state.KnownCardIds.ContainsKey(blockId))
					state.KnownCardIds[blockId] = new List<string>();
				state.KnownCardIds[blockId].Add(cardId);
			}
		}
	}
}
