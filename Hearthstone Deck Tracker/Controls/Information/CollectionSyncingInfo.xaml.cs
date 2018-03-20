using System.Windows.Input;
using Hearthstone_Deck_Tracker.Utility;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using HSReplay.OAuth;

namespace Hearthstone_Deck_Tracker.Controls.Information
{
	public partial class CollectionSyncingInfo
	{
		public CollectionSyncingInfo()
		{
			InitializeComponent();
		}

		public ICommand LoginCommand => new Command(() =>
		{
			Core.MainWindow.FlyoutUpdateNotes.IsOpen = false;
			Helper.OptionsMain.TreeViewItemHSReplayCollection.IsSelected = true;
			Core.MainWindow.FlyoutOptions.IsOpen = true;
			var successUrl = Helper.BuildHsReplayNetUrl("decks", "collection_info", new[] { "modal=collection" });
			Core.HSReplay.OAuth.Authenticate(successUrl, null, Scope.FullAccess).Forget();
		});

		public ICommand CloseCommand => new Command(() => Core.MainWindow.FlyoutUpdateNotes.IsOpen = false);

		public ICommand DecksCommand => new Command(() =>
		{
			var url = Helper.BuildHsReplayNetUrl("decks", "update_notes");
			Helper.TryOpenUrl(url);
		});
	}
}
