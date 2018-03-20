using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.HsReplay;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.FlyoutControls.Options.HSReplay
{
	public partial class HSReplayCollection : INotifyPropertyChanged
	{
		private bool _collectionUpToDate;
		private bool _collectionUpdateThrottled;

		public HSReplayCollection()
		{
			InitializeComponent();
			Core.HSReplay.OAuth.Authenticated += Update;
			Core.HSReplay.OAuth.LoggedOut += Update;
			Core.HSReplay.Events.CollectionUploaded += CollectionUpdated;
			Core.HSReplay.Events.CollectionAlreadyUpToDate += CollectionUpdated;
			Core.HSReplay.Events.CollectionUploadThrottled += () =>
			{
				CollectionUpToDate = false;
				CollectionUpdateThrottled = true;
			};
			ConfigWrapper.CollectionSyncingChanged += () =>
				OnPropertyChanged(nameof(CollectionSyncingEnabled));
			ScheduledTaskRunner.Instance.Schedule(() => OnPropertyChanged(nameof(SyncAge)), TimeSpan.FromMinutes(1));
		}

		private void CollectionUpdated()
		{
			CollectionUpdateThrottled = false;
			CollectionUpToDate = true;
			OnPropertyChanged(nameof(CollectionSynced));
			OnPropertyChanged(nameof(SyncAge));
		}

		private void Update()
		{
			OnPropertyChanged(nameof(IsAuthenticated));
			OnPropertyChanged(nameof(CollectionSynced));
		}

		public bool IsAuthenticated => Core.HSReplay.OAuth.IsFullyAuthenticated;

		public bool CollectionSynced => Core.HSReplay.Account.CollectionState.Any();

		public bool CollectionUpToDate
		{
			get => _collectionUpToDate;
			set
			{
				_collectionUpToDate = value; 
				OnPropertyChanged();
			}
		}

		public bool CollectionUpdateThrottled
		{
			get => _collectionUpdateThrottled;
			set
			{
				_collectionUpdateThrottled = value; 
				OnPropertyChanged();
			}
		}

		public string SyncAge => CollectionSynced
			? LocUtil.GetAge(Core.HSReplay.Account.CollectionState.Values.Max(x => x.Date))
			: string.Empty;

		public object HSReplayDecksCommand => new Command(()
			=> HSReplayNetHelper.OpenDecksUrlWithCollection("collection_syncing_banner"));

		public bool CollectionSyncingEnabled
		{
			get => ConfigWrapper.CollectionSyncingEnabled;
			set => ConfigWrapper.CollectionSyncingEnabled = value;
		}

		public string HSReplayDecksUrl =>
			Helper.BuildHsReplayNetUrl("decks", "oauth_login", new[] { "modal=collection" });

		public void UpdateSyncAge() => OnPropertyChanged(nameof(SyncAge));

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
