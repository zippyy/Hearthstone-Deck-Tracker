#region

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Utility.Attributes;
using HearthSim.Core.Hearthstone.Entities;
using static System.Windows.Visibility;
using static HearthDb.CardIds;

#endregion

namespace Hearthstone_Deck_Tracker.Controls
{
	public partial class CardMarker : INotifyPropertyChanged
	{
		private int _cardAge;
		private Visibility _cardAgeVisibility;
		private int _costReduction;
		private Visibility _costReductionVisibility;
		private BitmapImage _icon;
		private Visibility _iconVisibility;
		private ScaleTransform _scaleTransform;

		public CardMarker()
		{
			InitializeComponent();
			_scaleTransform = new ScaleTransform(1, 1);
		}

		public int CardAge
		{
			get => _cardAge;
			set
			{
				_cardAge = value;
				OnPropertyChanged();
			}
		}

		public Visibility CardAgeVisibility
		{
			get => _cardAgeVisibility;
			set
			{
				_cardAgeVisibility = value;
				OnPropertyChanged();
			}
		}

		public BitmapImage Icon
		{
			get => _icon;
			set
			{
				_icon = value;
				OnPropertyChanged();
			}
		}

		public Visibility IconVisibility
		{
			get => _iconVisibility;
			set
			{
				_iconVisibility = value;
				OnPropertyChanged();
			}
		}

		public Visibility CostReductionVisibility
		{
			get => _costReductionVisibility;
			set
			{
				_costReductionVisibility = value;
				OnPropertyChanged();
			}
		}

		public int CostReduction
		{
			get => _costReduction;
			set
			{
				_costReduction = value;
				OnPropertyChanged();
			}
		}

		public ScaleTransform ScaleTransform
		{
			get => _scaleTransform;
			set
			{
				_scaleTransform = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void UpdateIcon(Entity entity)
		{
			var mark = CardMark.None;
			if(entity.Info.Hidden)
				mark = CardMark.None;
			if(entity.CardId == NonCollectible.Neutral.TheCoin
				|| entity.CardId == NonCollectible.Neutral.TradePrinceGallywix_GallywixsCoinToken)
				mark = CardMark.Coin;
			if(entity.Info.ReturnedToHand)
				mark = CardMark.Returned;
			if(entity.IsCreated || entity.Info.Stolen)
				mark = CardMark.Created;
			if(entity.Info.MulliganDraw)
				mark =  CardMark.Mulliganed;
			UpdateIcon(mark);
		}

		public void UpdateIcon(CardMark mark)
		{
			if(Helper.TryGetAttribute<AssetNameAttribute>(mark, out var assetName) && assetName.Name != null)
			{
				var path = Path.Combine("/HearthstoneDeckTracker;component", assetName.Name);
				Icon = new BitmapImage(new Uri(path, UriKind.Relative));
				IconVisibility = Visible;
			}
			else
				IconVisibility = Collapsed;
		}

		public void UpdateCardAge(int? cardAge)
		{
			if(cardAge.HasValue)
			{
				CardAge = cardAge.Value;
				CardAgeVisibility = Visible;
			}
			else
				CardAgeVisibility = Collapsed;
		}

		public void UpdateCostReduction(int costReduction)
		{
			CostReduction = -costReduction;
			CostReductionVisibility = costReduction > 0 ? Visible : Collapsed;
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
