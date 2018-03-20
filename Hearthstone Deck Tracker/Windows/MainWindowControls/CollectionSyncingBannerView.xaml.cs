using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.Windows.MainWindowControls
{
	public partial class CollectionSyncingBannerView : INotifyPropertyChanged
	{
		public CollectionSyncingBannerView()
		{
			InitializeComponent();
			Core.HSReplay.Events.CollectionUploaded += Update;
			Core.HSReplay.Events.CollectionAlreadyUpToDate += Update;
			Core.HSReplay.OAuth.LoggedOut += Update;
			Core.HSReplay.OAuth.Authenticated += Update;
			ScheduledTaskRunner.Instance.Schedule(() => OnPropertyChanged(nameof(SyncAge)), TimeSpan.FromMinutes(1));
		}

		public bool CollectionSynced => Core.HSReplay.Account.CollectionState.Any();

		public bool IsAuthenticated => Core.HSReplay.OAuth.IsFullyAuthenticated;

		public string SyncAge => CollectionSynced
			? LocUtil.GetAge(Core.HSReplay.Account.CollectionState.Values.Max(x => x.Date))
			: string.Empty;

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void Update()
		{
			OnPropertyChanged(nameof(SyncAge));
			OnPropertyChanged(nameof(CollectionSynced));
			OnPropertyChanged(nameof(IsAuthenticated));
		}
	}
}
