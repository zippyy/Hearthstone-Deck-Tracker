using System;
using System.Linq;
using System.Threading.Tasks;
using HearthSim.Core.HSReplay.Data;
using WinrateData = System.Collections.Generic.Dictionary<string, HearthSim.Core.HSReplay.Data.DeckWinrateData>;

namespace Hearthstone_Deck_Tracker.HsReplay.Data
{
	public class HsReplayWinrates : JsonCache<WinrateData>
	{
		private bool _cleaned;
		private WinrateData _data;
		private async Task<WinrateData> GetData() => _data ?? (_data = await LoadFromDisk() ?? new WinrateData());

		public HsReplayWinrates() : base("hsreplay_winrates.cache")
		{
		}

		public async Task<DeckWinrateData> Get(string shortId, bool wild)
		{
			var data = await GetData();
			if(data.TryGetValue(shortId, out var deck) && !deck.IsStale)
				return deck;
			if(!_cleaned)
				Cleanup();
			var response = await Core.HSReplay.Api.GetDeckWinrates(shortId, wild);
			deck = response.Success ? response.Data ?? NoDataFallback : NoDataFallback;
			_data[shortId] = deck;
			await WriteToDisk(data);
			return deck;
		}

		private void Cleanup()
		{
			var stale = _data.Where(x => x.Value.IsStale).ToList();
			foreach(var s in stale)
				_data.Remove(s.Key);
			_cleaned = true;
		}

		public DeckWinrateData NoDataFallback => new DeckWinrateData
		{
			ClientTimeStamp = DateTime.Now.Subtract(TimeSpan.FromHours(23.5))
		};
	}
}
