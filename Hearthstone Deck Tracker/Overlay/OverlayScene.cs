using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Overlay.Config;

namespace Hearthstone_Deck_Tracker.Overlay
{
	public class OverlayScene : Canvas
	{
		private readonly SceneConfig _config;
		public List<UIElement> ClickableElements = new List<UIElement>();

		public static readonly DependencyProperty ClickableProperty = DependencyProperty.RegisterAttached("Clickable", typeof(bool), typeof(OverlayScene), new PropertyMetadata(default(bool)));
		public static readonly DependencyProperty DraggingEnabledProperty = DependencyProperty.RegisterAttached("DraggingEnabled", typeof(bool), typeof(OverlayScene), new PropertyMetadata(default(bool)));

		public static bool GetClickable(UIElement element)
		{
			return (bool) element.GetValue(ClickableProperty);
		}

		public static void SetClickable(UIElement element, bool value)
		{
			element.SetValue(ClickableProperty, value);
		}

		public static bool GetDraggable(UIElement element)
		{
			return (bool) element.GetValue(DraggingEnabledProperty);
		}

		public static void SetDraggable(UIElement element, bool value)
		{
			element.SetValue(DraggingEnabledProperty, value);
		}

		public static bool GetHoverable(UIElement element)
		{
			return (bool) element.GetValue(HoverableProperty);
		}

		public static void SetHoverable(UIElement element, bool value)
		{
			element.SetValue(HoverableProperty, value);
		}

		public bool DraggingEnabled { get; set; }
		private UIElement _dragging;
		private Point _draggingPoint;
		public static readonly DependencyProperty HoverableProperty = DependencyProperty.RegisterAttached("Hoverable", typeof(bool), typeof(OverlayScene), new PropertyMetadata(default(bool)));

		protected OverlayScene(SceneConfig config)
		{
			_config = config;
			MouseDown += OverlayCanvas_OnMouseDown;
			MouseMove += OverlayCanvas_OnMouseMove;
			MouseUp += OverlayCanvas_OnMouseUp;

			foreach(var element in config.Elements)
			{
				var instance = Activator.CreateInstance(
					"HearthstoneDeckTracker",
					"Hearthstone_Deck_Tracker." + element.Type
				);
				var uielement = (UIElement)instance.Unwrap();
				uielement.Uid = element.Name;
				Children.Add(uielement);
			}
		}

		~OverlayScene()
		{
			MouseDown -= OverlayCanvas_OnMouseDown;
			MouseMove -= OverlayCanvas_OnMouseMove;
			MouseUp -= OverlayCanvas_OnMouseUp;
		}

		private void OverlayCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if(!DraggingEnabled)
				return;
			var element = GetInteractableElementAt(e.GetPosition(this), OverlayInteraction.Drag);
			_dragging = element;
			if(element != null)
				_draggingPoint = e.GetPosition(element);
		}

		private void OverlayCanvas_OnMouseMove(object sender, MouseEventArgs e)
		{
			var pos = e.GetPosition(this);
			if(_dragging == null || !DraggingEnabled)
				return;
			var newTop = pos.Y - _draggingPoint.Y;
			var newLeft = pos.X - _draggingPoint.X;
			SetTop(_dragging, newTop);
			SetLeft(_dragging, newLeft);
		}

		private void OverlayCanvas_OnMouseUp(object sender, MouseButtonEventArgs e)
		{
			if(_dragging == null)
				return;
			var configItem = _config.Elements.FirstOrDefault(x => x.Name == _dragging.Uid);
			if(configItem != null)
			{
				configItem.Top = GetTop(_dragging) / ActualHeight * 100;
				configItem.Left = GetLeft(_dragging) / ActualWidth * 100;
			}
			_dragging = null;
		}

		private UIElement GetInteractableElementAt(Point point, OverlayInteraction interaction)
		{
			var infos = GetInteractableChildren(this, interaction).ToList();
			return infos.FirstOrDefault(x => point.X > x.Left && point.X < x.Right && point.Y > x.Top && point.Y < x.Bottom)?.Element;
		}

		private IEnumerable<UIElementInfo> GetInteractableChildren(Canvas canvas, OverlayInteraction interaction)
		{
			foreach(var child in canvas.Children.OfType<UIElement>())
			{
				if(interaction == OverlayInteraction.Click && GetClickable(child))
					yield return GetInfo(child);
				else if(interaction == OverlayInteraction.Drag && GetDraggable(child))
					yield return GetInfo(child);
				else if(interaction == OverlayInteraction.Hover)
				{
					var config = _config.Elements.FirstOrDefault(e => e.Name == child.Uid);
					if(config?.HasInteractableChildren ?? false)
					{
						var hoverable = Helper.FindVisualChildren<UIElement>(child).Where(x => x is IHoverable h && h.Hoverable).ToList();
						foreach(var h in hoverable)
							yield return GetInfo(h, this);
					}
				}
			}
		}

		private UIElementInfo GetInfo(UIElement element)
		{
			var size = element.RenderSize;
			return new UIElementInfo
			{
				Element = element,
				Top = GetTop(element),
				Left = GetLeft(element),
				Right = GetLeft(element) + size.Width,
				Bottom = GetTop(element) + size.Height
			};
		}

		private UIElementInfo GetInfo(UIElement element, Panel parent)
		{
			var size = element.RenderSize;
			var pos = element.TranslatePoint(new Point(0, 0), parent);
			return new UIElementInfo
			{
				Element = element,
				Top = pos.Y,
				Left = pos.X,
				Right = pos.X + size.Width,
				Bottom = pos.Y + size.Height
			};
		}

		public UIElement GetInteractableElement()
		{
			if(!IsVisible) 
				return null;
			var cursor = User32.GetMousePos();
			var relativeCursor = PointFromScreen(new Point(cursor.X, cursor.Y));
			return GetInteractableElementAt(relativeCursor, OverlayInteraction.Click);
		}

		protected internal virtual void Update()
		{
			if(_dragging != null)
				return;
			var elements = Children.OfType<UIElement>()
				.Select(e => new { Element = e, Config = _config.Elements.FirstOrDefault(ce => ce.Name == e.Uid) })
				.Where(e => e.Config != null);
			foreach(var element in elements)
			{
				SetTop(element.Element, element.Config.Top / 100 * ActualHeight);
				SetLeft(element.Element, element.Config.Left / 100 * ActualWidth);
			}
			if(IsVisible && !DraggingEnabled)
			{
				var cursor = User32.GetMousePos();
				var relativeCursor = PointFromScreen(new Point(cursor.X, cursor.Y));
				var uiElement = GetInteractableElementAt(relativeCursor, OverlayInteraction.Hover) as IHoverable;
				uiElement?.OnHover();
			}
		}
	}

	public interface IHoverable
	{
		bool Hoverable { get; }
		void OnHover();
	}
}
