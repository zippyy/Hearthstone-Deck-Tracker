using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Utility;
using HearthSim.Core.HSReplay;
using HearthSim.Core.HSReplay.Data;
using static System.Windows.Visibility;

namespace Hearthstone_Deck_Tracker.FlyoutControls.Options.HSReplay
{
	public partial class HSReplayAccount : INotifyPropertyChanged
	{
		private bool _logoutButtonEnabled = true;
		private bool _logoutTriggered;
		private bool _claimTokenButtonEnabled = true;

		private OAuthWrapper HSReplayNetOAuth => Core.HSReplay.OAuth;
		private Account Account => Core.HSReplay.Account;

		public HSReplayAccount()
		{
			InitializeComponent();
			HSReplayNetOAuth.AccountDataUpdated += () =>
			{
				Update();
				LogoutTriggered = false;
			};
			HSReplayNetOAuth.LoggedOut += () =>
			{
				Update();
				LogoutTriggered = false;
			};
			HSReplayNetOAuth.UploadTokenClaimed += () => OnPropertyChanged(nameof(UploadTokenUnclaimed));
			Account.TokenStatusChanged += args => OnPropertyChanged(nameof(UploadTokenUnclaimed));
			ConfigWrapper.ReplayAutoUploadChanged += () => OnPropertyChanged(nameof(ReplayUploadingEnabled));
			ConfigWrapper.CollectionSyncingChanged += () => OnPropertyChanged(nameof(CollectionSyncingEnabled));
		}

		public bool IsAuthenticated => HSReplayNetOAuth.IsFullyAuthenticated;

		public Visibility ReplaysClaimedVisibility =>
			Account.Status == AccountStatus.Anonymous|| HSReplayNetOAuth.IsFullyAuthenticated ? Collapsed : Visible;

		public Visibility LoginInfoVisibility =>
			Account.Status == AccountStatus.Anonymous && !HSReplayNetOAuth.IsFullyAuthenticated ? Visible : Collapsed;

		public bool IsPremiumUser =>
			HSReplayNetOAuth.AccountData?.IsPremium?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false;

		public Visibility LogoutWarningVisibility => LogoutTriggered ? Visible : Collapsed;

		public bool LogoutTriggered
		{
			get => _logoutTriggered;
			set
			{
				_logoutTriggered = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(LogoutWarningVisibility));
			}
		}

		public bool LogoutButtonEnabled
		{
			get => _logoutButtonEnabled;
			set
			{
				_logoutButtonEnabled = value; 
				OnPropertyChanged();
			}
		}

		public string Username => HSReplayNetOAuth.AccountData?.Username ?? Account.Username ?? string.Empty;

		public ICommand LogoutCommand => new Command(async () =>
		{
			if(LogoutTriggered)
			{
				LogoutButtonEnabled = false;
				await HSReplayNetOAuth.Logout();
				LogoutButtonEnabled = true;
			}
			else
				LogoutTriggered = true;
		});

		public bool ClaimTokenButtonEnabled
		{
			get => _claimTokenButtonEnabled;
			set
			{
				_claimTokenButtonEnabled = value; 
				OnPropertyChanged();
			}
		}

		public ICommand EnableCollectionSyncingCommand => new Command(() => ConfigWrapper.CollectionSyncingEnabled = true);

		public ICommand EnableReplayUploadingCommand => new Command(() => ConfigWrapper.HsReplayAutoUpload = true);

		public ICommand PremiumInfoCommand => new Command(() =>
		{
			var url = Helper.BuildHsReplayNetUrl("premium", "options_account_premium_info");
			Helper.TryOpenUrl(url);
		});

		public ICommand AccountSettingsCommand => new Command(() =>
		{
			var url = Helper.BuildHsReplayNetUrl("account", "options_account_settings");
			Helper.TryOpenUrl(url);
		});

		public ICommand ClaimUploadTokenCommand => new Command(async () =>
		{
			ClaimTokenButtonEnabled = false;
			if(Account.TokenStatus == TokenStatus.Unknown)
				await Core.HSReplay.Api.UpdateTokenStatus();
			if(Account.TokenStatus == TokenStatus.Unclaimed)
				await HSReplayNetOAuth.ClaimUploadToken(Account.UploadToken);
			ClaimTokenButtonEnabled = true;
		});

		public bool ReplayUploadingEnabled => ConfigWrapper.HsReplayAutoUpload;

		public bool CollectionSyncingEnabled => ConfigWrapper.CollectionSyncingEnabled;

		public bool UploadTokenUnclaimed => IsAuthenticated && Account.TokenStatus == TokenStatus.Unclaimed;

		public void Update()
		{
			OnPropertyChanged(nameof(Username));
			OnPropertyChanged(nameof(IsAuthenticated));
			OnPropertyChanged(nameof(ReplaysClaimedVisibility));
			OnPropertyChanged(nameof(LoginInfoVisibility));
			OnPropertyChanged(nameof(IsPremiumUser));
			OnPropertyChanged(nameof(UploadTokenUnclaimed));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
