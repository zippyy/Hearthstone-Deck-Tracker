using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Overlay.Config;
using Hearthstone_Deck_Tracker.Overlay.Scenes;
using Rectangle = System.Drawing.Rectangle;

namespace Hearthstone_Deck_Tracker.Overlay
{
	public partial class OverlayWindow : INotifyPropertyChanged
	{
		private bool _transparent;
		private OverlayScene _activeScene;

		private static int WindowExStyle => User32.WsExToolWindow | User32.WsExTransparent;

		public OverlayScene ActiveScene
		{
			get => _activeScene;
			set
			{
				_activeScene = value;
				OnPropertyChanged();
			}
		}

		public OverlayWindow(OverlayConfig config)
		{
			InitializeComponent();
			ActiveScene = new DemoScene(config.Scenes.First());
		}

		private void OnSourceInitialized(object sender, EventArgs e)
		{
			SetTransparent(true);
		}

		private void SetTransparent(bool transparent)
		{
			if(ActiveScene.DraggingEnabled)
			{
				if(_transparent)
				{
					SetTransparentInternal(false);
				}
				if(Equals(Background, Brushes.Transparent))
					Background = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
				return;
			}
			Background = Brushes.Transparent;
			if(transparent == _transparent)
				return;
			SetTransparentInternal(transparent);
			_transparent = transparent;
		}

		private void SetTransparentInternal(bool transparent)
		{
			var hwnd = new WindowInteropHelper(this).Handle;
			if(transparent)
				User32.SetWindowExStyle(hwnd, WindowExStyle);
			else
				User32.RemoveWindowExStyle(hwnd, WindowExStyle);
		}


		public void Update(Rectangle rect, WindowState state)
		{
			Top = rect.Y;
			Left = rect.X;
			Width = rect.Width;
			Height = rect.Height;
			WindowState = state;
			var element = ActiveScene.GetInteractableElement();
			ActiveScene.Update();
			SetTransparent(element == null);
		}

		private void OnClosing(object sender, CancelEventArgs e)
		{
			e.Cancel = true;
			Hide();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
