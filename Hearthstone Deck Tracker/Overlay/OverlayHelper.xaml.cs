using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Overlay.Config;
using Hearthstone_Deck_Tracker.Overlay.Scenes;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.Overlay
{
	public partial class OverlayHelper : INotifyPropertyChanged
	{
		private List<Process> _processes;
		private Overlay _overlay;

		public OverlayHelper()
		{
			InitializeComponent();
			_overlay = new Overlay();
			_processes = new List<Process>();
			Update();
		}

		public List<Process> Processes
		{
			get => _processes;
			set
			{
				_processes = value;
				OnPropertyChanged();
			}
		}

		public Process SelectedProc { get; set; }

		public ICommand AttachCommand => new Command(() =>
		{
			if(SelectedProc != null)
				_overlay.Attach(SelectedProc.MainWindowHandle);
		});

		public ICommand DetachCommand => new Command(() => _overlay.Detach());

		public ICommand GenConfigCommand => new Command(() =>
		{
			var config = new OverlayConfig
			{
				Scenes = new List<SceneConfig>()
			};
			var scene = new SceneConfig
			{
				Scene = Scene.Demo,
				Elements = new List<ElementConfig>(),
			};
			var element = new ElementConfig
			{
				Left = 5,
				Name = "DemoElement1",
				Type = "DemoElement",
				Top = 5,
				ScreenRegion = ScreenRegion.Full
			};
			scene.Elements.Add(element);
			config.Scenes.Add(scene);
			XmlManager<OverlayConfig>.Save(Path.Combine(Hearthstone_Deck_Tracker.Config.AppDataPath, "OverlayConfig.xml"), config);
		});

		public ICommand ReloadConfigCommand => new Command(() =>
		{
			_overlay.Detach();
			_overlay = new Overlay();
			if(SelectedProc != null)
				_overlay.Attach(SelectedProc.MainWindowHandle);
		});

		private void Update()
		{
			var procs = Process.GetProcesses().Where(x => x.MainWindowHandle != IntPtr.Zero).ToList();
			if(procs.Count != Processes.Count || procs.Any(p => Processes.All(p2 => p2.Id != p.Id)))
			{
				Processes = procs.ToList();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
