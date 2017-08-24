using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker.Controls;
using Hearthstone_Deck_Tracker.Overlay.Config;

namespace Hearthstone_Deck_Tracker.Overlay.Scenes
{
	public partial class DemoScene
	{
		public DemoScene(SceneConfig config) : base(config)
		{
			InitializeComponent();
		}

		private void DragButton_OnClick(object sender, RoutedEventArgs e)
		{
			DraggingEnabled = !DraggingEnabled;
			BtnDragging.Content = DraggingEnabled ? "Disable Dragging" : "Enable Dragging";
		}

		private int _count;
		protected internal override void Update()
		{
			base.Update();
			if(++_count % 300 == 0)
				UpdatePlayerCards();
		}

		private Random _rnd = new Random();
		public void UpdatePlayerCards()
		{
			var decks = DeckList.Instance.Decks.Where(x => x.Class == "Druid").ToList();
			var deck = decks[_rnd.Next(0, decks.Count - 1)];
			var list = Children.OfType<UIElement>().FirstOrDefault(x => x.Uid == "PlayerCards") as AnimatedCardList;
			list?.Update(deck?.Cards.ToList(), false);
		}
	}
}
