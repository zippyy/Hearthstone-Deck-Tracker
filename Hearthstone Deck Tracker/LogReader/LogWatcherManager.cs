using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.LogReader.Handlers;
using Hearthstone_Deck_Tracker.Windows;
using HearthSim.Core.Util.EventArgs;
using static Hearthstone_Deck_Tracker.API.LogEvents;

namespace Hearthstone_Deck_Tracker.LogReader
{
	public class LogWatcherManager
	{
		private readonly PowerHandler _powerLineHandler = new PowerHandler();
		private readonly RachelleHandler _rachelleHandler = new RachelleHandler();
		private readonly LoadingScreenHandler _loadingScreenHandler = new LoadingScreenHandler();
		private readonly FullScreenFxHandler _fullScreenFxHandler = new FullScreenFxHandler();
		private HsGameState _gameState;

		public LogWatcherManager()
		{
			Core.LogReader.NewLines += OnNewLines;
			Core.LogReader.Starting += () =>
			{
				_gameState = new HsGameState(Core.Game) { GameHandler = new GameEventHandler(Core.Game) };
				_gameState.Reset();
			};
			Core.Hearthstone.GameCreated += args => _gameState.Reset();
			_loadingScreenHandler.OnHearthMirrorCheckFailed += OnHearthMirroCheckFailed;
		}

		private async void OnHearthMirroCheckFailed()
		{
			await Core.Manager.Stop();
			Core.MainWindow.ActivateWindow();
			while(Core.MainWindow.Visibility != Visibility.Visible || Core.MainWindow.WindowState == WindowState.Minimized)
				await Task.Delay(100);
			await Core.MainWindow.ShowMessage("Uneven permissions",
				"It appears that Hearthstone (Battle.net) and HDT do not have the same permissions."
				+ "\n\nPlease run both as administrator or local user."
				+ "\n\nIf you don't know what any of this means, just run HDT as administrator.");
		}

		private void OnNewLines(NewLinesEventArgs args)
		{
			foreach(var line in args.Lines)
			{
				Core.Game.GameTime.Time = line.Time;
				switch(line.LogName)
				{
					case "Power":
						if(line.Text.StartsWith("GameState."))
							Core.Game.PowerLog.Add(line.Text);
						else
						{
							_powerLineHandler.Handle(line.Text, _gameState, Core.Game);
							OnPowerLogLine.Execute(line.Text);
						}
						break;
					case "Rachelle":
						_rachelleHandler.Handle(line.Text, _gameState, Core.Game);
						OnRachelleLogLine.Execute(line.Text);
						break;
					case "LoadingScreen":
						_loadingScreenHandler.Handle(line, _gameState, Core.Game);
						break;
					case "FullScreenFX":
						_fullScreenFxHandler.Handle(line, Core.Game);
						break;
				}
			}
			Helper.UpdateEverything(Core.Game);
		}
	}
}
