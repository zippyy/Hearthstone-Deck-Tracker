using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Windows;
using HearthSim.Core.Hearthstone;

namespace Hearthstone_Deck_Tracker.API
{
	public class Core
	{
		public static Game Game => Hearthstone_Deck_Tracker.Core.Hearthstone;

		public static Canvas OverlayCanvas => Hearthstone_Deck_Tracker.Core.Overlay.CanvasInfo;

		public static OverlayWindow OverlayWindow => Hearthstone_Deck_Tracker.Core.Overlay;

		public static MainWindow MainWindow => Hearthstone_Deck_Tracker.Core.MainWindow;
	}
}
