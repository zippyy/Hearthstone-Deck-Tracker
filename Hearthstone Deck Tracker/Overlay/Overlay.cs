using System;
using System.IO;
using System.Threading.Tasks;
using Hearthstone_Deck_Tracker.Overlay.Config;

namespace Hearthstone_Deck_Tracker.Overlay
{
	public class Overlay
	{
		private readonly OverlayWindow _overlayWindow;
		private IntPtr _handle = IntPtr.Zero;
		private bool _updating;
		private readonly OverlayConfig _config;
		private const int Fps = 60;

		private string ConfigPath => Path.Combine(Hearthstone_Deck_Tracker.Config.AppDataPath, "OverlayConfig.xml");

		public Overlay()
		{
			_config = XmlManager<OverlayConfig>.Load(ConfigPath);
			_overlayWindow = new OverlayWindow(_config);
		}

		public void Attach(IntPtr windowHandle)
		{
			_handle = windowHandle;
			Update();
		}

		public void Detach()
		{
			_handle = IntPtr.Zero;
			XmlManager<OverlayConfig>.Save(ConfigPath, _config);
		}

		private async void Update()
		{
			if(_updating)
				return;
			_updating = true;
			while(_handle != IntPtr.Zero)
			{
				var rect = User32.GetWindowRect(_handle);
				var state = User32.GetWindowState(_handle);
				_overlayWindow.Update(rect, state);
				if(!_overlayWindow.IsVisible)
					_overlayWindow.Show();
				await Task.Delay(1000 / Fps);
			}
			_overlayWindow.Hide();
			_updating = false;
		}
	}
}
