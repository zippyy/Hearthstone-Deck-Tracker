using System.Threading.Tasks;
using System.Windows;

namespace Hearthstone_Deck_Tracker.Overlay.Elements
{
	public partial class DemoElement
	{
		public DemoElement()
		{
			InitializeComponent();
		}

		private async void CloseButton_OnClick(object sender, RoutedEventArgs e)
		{
			TestChild.Visibility = Visibility.Collapsed;
			await Task.Delay(5000);
			TestChild.Visibility = Visibility.Visible;
		}
	}
}
