using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using HearthSim.Util;

namespace Hearthstone_Deck_Tracker.Controls
{
	public partial class StartHearthstoneButton : INotifyPropertyChanged
	{
		public StartHearthstoneButton()
		{
			InitializeComponent();
			HearthstoneRunner.StartingHearthstone += state => Enabled = state != HearthstoneRunner.State.Starting;
			Core.Hearthstone.HearthstoneStarted += () => OnPropertyChanged(nameof(HearthstoneIsRunning));
			Core.Hearthstone.HearthstoneExited += () => OnPropertyChanged(nameof(HearthstoneIsRunning));
		}

		private bool _enabled = true;

		public bool Enabled
		{
			get => _enabled;
			set
			{
				if(_enabled != value)
				{
					_enabled = value;
					OnPropertyChanged();
				}
			}
		}

		public bool HearthstoneIsRunning => Core.Hearthstone.IsRunning;

		public ICommand StartHearthstone => new Command(() => HearthstoneRunner.StartHearthstone().Forget());
		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
