#region

using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.LogReader.Handlers;
using Hearthstone_Deck_Tracker.LogReader.Interfaces;
using Hearthstone_Deck_Tracker.Replay;
using Hearthstone_Deck_Tracker.Utility.Logging;

#endregion

namespace Hearthstone_Deck_Tracker.LogReader
{
	public class LogState : ILogState
	{
		private readonly GameV2 _game;

		public LogState(GameV2 game)
		{
			_game = game;
			_game.OnPlayersFound += GameOnOnPlayersFound;
			KnownCardIds = new Dictionary<int, IList<string>>();
		}

		private void GameOnOnPlayersFound()
		{
			var count = TmpEntities.Count;
			if(count == 0)
				return;
			foreach(var tmpEntity in TmpEntities.ToList())
			{
				if(tmpEntity.Name == _game.Player.Name)
					TransferTempData(tmpEntity, _game.PlayerEntity);
				else if (tmpEntity.Name == _game.Opponent.Name)
					TransferTempData(tmpEntity, _game.OpponentEntity);
			}
			if(TmpEntities.Count < count)
			{
				Log.Info("Invoking queued actions");
				TagChangeHandler.InvokeQueuedActions(_game);
			}
		}

		public bool CurrentEntityHasCardId { get; set; }
		public int CurrentEntityId { get; private set; }
		public bool GameEnded { get; set; }
		public IGameHandler GameHandler { get; set; }
		public DateTime LastGameStart { get; set; }
		public int LastId { get; set; }
		public bool OpponentUsedHeroPower { get; set; }
		public bool PlayerUsedHeroPower { get; set; }
		public bool FoundSpectatorStart { get; set; }
		public int JoustReveals { get; set; }
		public Dictionary<int, IList<string>> KnownCardIds { get; set; }
		public int LastCardPlayed { get; set; }
		public bool WasInProgress { get; set; }
		public bool SetupDone { get; set; }
		public int GameTriggerCount { get; set; }
		public Zone CurrentEntityZone { get; set; }
		public bool DeterminedPlayers => _game.Player.Id > 0 && _game.Opponent.Id > 0;
		public List<Entity> TmpEntities { get; } = new List<Entity>();
		public TagChangeHandler TagChangeHandler { get; } = new TagChangeHandler();

		public int GetTurnNumber()
		{
			if(!_game.IsMulliganDone)
				return 0;
			return (_game.GameEntity?.GetTag(GameTag.TURN) + 1) / 2 ?? 0;
		}

		public void Reset()
		{
			GameEnded = false;
			JoustReveals = 0;
			KnownCardIds.Clear();
			LastGameStart = DateTime.Now;
			WasInProgress = false;
			SetupDone = false;
			CurrentEntityId = 0;
			GameTriggerCount = 0;
			CurrentBlock = null;
			_maxBlockId = 0;
			TmpEntities.Clear();
			TagChangeHandler.ClearQueuedActions();
		}

		public void SetCurrentEntity(int id)
		{
			CurrentEntityId = id;
		}

		public void ResetCurrentEntity() => CurrentEntityId = 0;

		private int _maxBlockId;
		public Block CurrentBlock { get; private set; }

		public void BlockStart(string type)
		{
			var blockId = _maxBlockId++;
			CurrentBlock = CurrentBlock?.CreateChild(blockId, type) ?? new Block(null, blockId, type);
		}

		public void BlockEnd()
		{
			CurrentBlock = CurrentBlock?.Parent;
			if(_game.Entities.TryGetValue(CurrentEntityId, out var entity))
				entity.Info.HasOutstandingTagChanges = false;
		}

		public void TransferTempData(Entity tmpEntity, Entity entity)
		{
			Log.Info($"Transferring {tmpEntity.Tags.Count} tags from temp entity ({tmpEntity.Id}) to {tmpEntity.Name}");
			entity.Name = tmpEntity.Name;
			foreach(var tag in tmpEntity.Tags)
				TagChangeHandler.TagChange(this, tag.Key, entity.Id, tag.Value, _game);
			TmpEntities.Remove(tmpEntity);
		}
	}

	public class Block
	{
		public Block Parent { get; }
		public IList<Block> Children { get; }
		public int Id { get; }
		public string Type { get; }

		public Block(Block parent, int blockId, string type)
		{
			Parent = parent;
			Children = new List<Block>();
			Id = blockId;
			Type = type;
		}

		public Block CreateChild(int blockId, string type) => new Block(this, blockId, type);
	}
}
