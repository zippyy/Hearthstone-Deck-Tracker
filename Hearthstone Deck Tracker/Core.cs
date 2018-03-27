using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Controls.Stats;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Analytics;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using Hearthstone_Deck_Tracker.Utility.HotKeys;
using Hearthstone_Deck_Tracker.Utility.LogConfig;
using Hearthstone_Deck_Tracker.Windows;
using MahApps.Metro.Controls.Dialogs;
using Hearthstone_Deck_Tracker.Utility.Themes;
using Hearthstone_Deck_Tracker.Utility.Updating;
using HearthSim.Core;
using HearthSim.Core.HSReplay;
using HearthSim.Core.LogReading;
using WPFLocalizeExtension.Engine;
using Log = HearthSim.Util.Logging.Log;

namespace Hearthstone_Deck_Tracker
{
	public static class Core
	{
		internal const int UpdateDelay = 100;
		private static TrayIcon _trayIcon;
		private static OverlayWindow _overlay;
		private static Overview _statsOverview;
		private static int _updateRequestsPlayer;
		private static int _updateRequestsOpponent;
		private static DateTime _startUpTime;
		public static Version Version { get; set; }
		public static MainWindow MainWindow { get; set; }

		internal static HSReplayNet HSReplay => Manager?.HSReplayNet;
		internal static HearthSim.Core.Hearthstone.Game Hearthstone => Manager?.Game;
		internal static ILogInput LogReader => Manager?.LogReader;
		internal static Manager Manager { get; private set; }

		public static Overview StatsOverview => _statsOverview ?? (_statsOverview = new Overview());

		public static bool Initialized { get; private set; }

		public static TrayIcon TrayIcon => _trayIcon ?? (_trayIcon = new TrayIcon());

		public static OverlayWindow Overlay => _overlay ?? (_overlay = new OverlayWindow(Hearthstone));

		internal static bool UpdateOverlay { get; set; } = true;
		internal static bool Update { get; set; }
		internal static bool CanShutdown { get; set; }

#pragma warning disable 1998
		public static async void Initialize()
#pragma warning restore 1998
		{
			LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
			_startUpTime = DateTime.UtcNow;
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
			Config.Load();
			var hsReplayConfig = new HSReplayNetConfig(Config.Instance.DataDir, "089b2bc6-3c26-4aab-adbe-bcfd5bb48671",
				"jIpNwuUWLFI6S3oeQkO3xlW6UCnfogw1IpAbFXqq", Helper.GetUserAgent(), GameTypeHelper.All,
				Config.Instance.HsReplayUploadPacks ?? false, Config.Instance.SelectedTwitchUser);
			Manager = new Manager(hsReplayConfig);
			Manager.Start();

			//TODO: Figure out where to put these
			Manager.Game.FriendlyChallenge += () =>
			{
				if(Config.Instance.FlashHsOnFriendlyChallenge)
					User32.FlashHs();
			};
			Manager.Game.DungeonRunMatchStarted += args =>
			{
				DeckManager.DungeonRunMatchStarted(args.IsNew, args.Deck); 
			};
			Manager.Game.DungeonRunDeckUpdated += args =>
			{
				DeckManager.UpdateDungeonRunDeck(args.Deck); 
			};
			Manager.Game.Arena.DraftComplete += args =>
			{
				var behaviour = Config.Instance.SelectedArenaImportingBehaviour
								?? ArenaImportingBehaviour.AutoImportSave;
				DeckManager.AutoImportArena(behaviour, args.Info);
			};
			Manager.Game.HearthstoneInstallationNotFound += () =>
			{
				ErrorManager.AddError("Could not find Hearthstone installation",
					"Please set Hearthstone installation path via 'options > tracker > settings > set hearthstone path'.");
			};

			Log.Info($"HDT: {Helper.GetCurrentVersion()}, Operating System: {Helper.GetWindowsVersion()}, .NET Framework: {Helper.GetInstalledDotNetVersion()}");
			var splashScreenWindow = new SplashScreenWindow();
#if(SQUIRREL)
			if(Config.Instance.CheckForUpdates)
			{
				var updateCheck = Updater.StartupUpdateCheck(splashScreenWindow);
				while(!updateCheck.IsCompleted)
				{
					await Task.Delay(500);
					if(splashScreenWindow.SkipUpdate)
						break;
				}
			}
#endif
			splashScreenWindow.ShowConditional();
			Log.Initialize(Path.Combine(Config.Instance.DataDir, "Logs"), "hdt_log");
			ConfigManager.Run();
			LocUtil.UpdateCultureInfo();
			var newUser = ConfigManager.PreviousVersion == null;
			LogConfigUpdater.Run().Forget();
			LogConfigWatcher.Start();
			UITheme.InitializeTheme();
			ThemeManager.Run();
			ResourceMonitor.Run();
			//Game.SecretsManager.OnSecretsChanged += cards => Overlay.ShowSecrets(cards);
			MainWindow = new MainWindow();
			MainWindow.LoadConfigSettings();
			MainWindow.Show();
			splashScreenWindow.Close();

			if(Config.Instance.DisplayHsReplayNoteLive && ConfigManager.PreviousVersion != null && ConfigManager.PreviousVersion < new Version(1, 1, 0))
				MainWindow.FlyoutHsReplayNote.IsOpen = true;

			if(ConfigManager.UpdatedVersion != null)
			{
#if(!SQUIRREL)
				Updater.Cleanup();
#endif
				MainWindow.FlyoutUpdateNotes.IsOpen = true;
				MainWindow.UpdateNotesControl.SetHighlight(ConfigManager.PreviousVersion);
#if(SQUIRREL && !DEV)
				if(Config.Instance.CheckForDevUpdates && !Config.Instance.AllowDevUpdates.HasValue)
					MainWindow.ShowDevUpdatesMessage();
#endif
			}
			DataIssueResolver.Run();

#if(!SQUIRREL)
			Helper.CopyReplayFiles();
#endif
			BackupManager.Run();

			if(Config.Instance.PlayerWindowOnStart)
				Windows.PlayerWindow.Show();
			if(Config.Instance.OpponentWindowOnStart)
				Windows.OpponentWindow.Show();
			if(Config.Instance.TimerWindowOnStartup)
				Windows.TimerWindow.Show();

			PluginManager.Instance.LoadPluginsFromDefaultPath();
			MainWindow.Options.OptionsTrackerPlugins.Load();
			PluginManager.Instance.StartUpdateAsync();

			UpdateOverlayAsync();

			if(Config.Instance.ShowCapturableOverlay)
			{
				Windows.CapturableOverlay = new CapturableOverlayWindow();
				Windows.CapturableOverlay.Show();
			}

			if(LogConfigUpdater.LogConfigUpdateFailed)
				MainWindow.ShowLogConfigUpdateFailedMessage().Forget();
			else if(LogConfigUpdater.LogConfigUpdated && Hearthstone.IsRunning)
			{
				MainWindow.ShowMessageAsync("Hearthstone restart required", "The log.config file has been updated. HDT may not work properly until Hearthstone has been restarted.").Forget();
				Overlay.ShowRestartRequiredWarning();
			}

			RemoteConfig.Instance.Load();
			HotKeyManager.Load();

			if(Helper.HearthstoneDirExists && Config.Instance.StartHearthstoneWithHDT && !Hearthstone.IsRunning)
				HearthstoneRunner.StartHearthstone().Forget();

			Initialized = true;

			Influx.OnAppStart(
				Helper.GetCurrentVersion(),
				newUser,
				HSReplay.OAuth.IsFullyAuthenticated,
				HSReplay.OAuth.AccountData?.IsPremium?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false,
				(int)(DateTime.UtcNow - _startUpTime).TotalSeconds,
				PluginManager.Instance.Plugins.Count
			);
		}

		private static async void UpdateOverlayAsync()
		{
#if(!SQUIRREL)
			if(Config.Instance.CheckForUpdates)
				Updater.CheckForUpdates(true);
#endif
			var hsForegroundChanged = false;
			while(UpdateOverlay)
			{
				if(Config.Instance.CheckForUpdates)
					Updater.CheckForUpdates();
				if(User32.GetHearthstoneWindow() != IntPtr.Zero)
				{
					// TODO: on hearthstone start
					//		BackupManager.Run();
					Overlay.UpdatePosition();

					if(!Hearthstone.IsRunning)
					{
						Overlay.Update(true);
						Windows.CapturableOverlay?.UpdateContentVisibility();
					}

					TrayIcon.MenuItemStartHearthstone.Visible = false;

					Helper.GameWindowState = User32.GetHearthstoneWindowState();
					Windows.CapturableOverlay?.Update();
					if(User32.IsHearthstoneInForeground() && Helper.GameWindowState != WindowState.Minimized)
					{
						if(hsForegroundChanged)
						{
							Overlay.Update(true);
							if(Config.Instance.WindowsTopmostIfHsForeground && Config.Instance.WindowsTopmost)
							{
								//if player topmost is set to true before opponent:
								//clicking on the playerwindow and back to hs causes the playerwindow to be behind hs.
								//other way around it works for both windows... what?
								Windows.OpponentWindow.Topmost = true;
								Windows.PlayerWindow.Topmost = true;
								Windows.TimerWindow.Topmost = true;
							}
							hsForegroundChanged = false;
						}
					}
					else if(!hsForegroundChanged)
					{
						if(Config.Instance.WindowsTopmostIfHsForeground && Config.Instance.WindowsTopmost)
						{
							Windows.PlayerWindow.Topmost = false;
							Windows.OpponentWindow.Topmost = false;
							Windows.TimerWindow.Topmost = false;
						}
						hsForegroundChanged = true;
					}
				}
				else if(Hearthstone.IsRunning)
				{
					Overlay.ShowOverlay(false);
					if(Windows.CapturableOverlay != null)
					{
						Windows.CapturableOverlay.UpdateContentVisibility();
						await Task.Delay(100);
						Windows.CapturableOverlay.ForcedWindowState = WindowState.Minimized;
						Windows.CapturableOverlay.WindowState = WindowState.Minimized;
					}
					Overlay.HideRestartRequiredWarning();
					Helper.ClearCachedHearthstoneBuild();
					TurnTimer.Instance.Stop();

					TrayIcon.MenuItemStartHearthstone.Visible = true;

					if(Config.Instance.CloseWithHearthstone)
						MainWindow.Close();
				}

				await Task.Delay(UpdateDelay);
			}
			CanShutdown = true;
		}

		internal static async void UpdatePlayerCards(bool reset = false)
		{
			_updateRequestsPlayer++;
			await Task.Delay(100);
			_updateRequestsPlayer--;
			if(_updateRequestsPlayer > 0)
				return;
			//Overlay.UpdatePlayerCards(new List<Card>(Game.Player.PlayerCardList), reset);
			//if(Windows.PlayerWindow.IsVisible)
			//	Windows.PlayerWindow.UpdatePlayerCards(new List<Card>(Game.Player.PlayerCardList), reset);
		}

		internal static async void UpdateOpponentCards(bool reset = false)
		{
			_updateRequestsOpponent++;
			await Task.Delay(100);
			_updateRequestsOpponent--;
			if(_updateRequestsOpponent > 0)
				return;
			//Overlay.UpdateOpponentCards(new List<Card>(Game.Opponent.OpponentCardList), reset);
			//if(Windows.OpponentWindow.IsVisible)
			//	Windows.OpponentWindow.UpdateOpponentCards(new List<Card>(Game.Opponent.OpponentCardList), reset);
		}

		public static class Windows
		{
			private static PlayerWindow _playerWindow;
			private static OpponentWindow _opponentWindow;
			private static TimerWindow _timerWindow;
			private static StatsWindow _statsWindow;

			//TODO
			public static PlayerWindow PlayerWindow => _playerWindow ?? (_playerWindow = new PlayerWindow(Hearthstone));
			public static OpponentWindow OpponentWindow => _opponentWindow ?? (_opponentWindow = new OpponentWindow(Hearthstone));

			public static TimerWindow TimerWindow => _timerWindow ?? (_timerWindow = new TimerWindow(Config.Instance));
			public static StatsWindow StatsWindow => _statsWindow ?? (_statsWindow = new StatsWindow());
			public static CapturableOverlayWindow CapturableOverlay;
		}

		internal static bool StatsOverviewInitialized => _statsOverview != null;
		public static List<long> IgnoredArenaDecks { get; } = new List<long>();
	}
}
