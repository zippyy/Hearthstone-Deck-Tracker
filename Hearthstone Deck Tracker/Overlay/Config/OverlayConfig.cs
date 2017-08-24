using System.Collections.Generic;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Overlay.Scenes;

namespace Hearthstone_Deck_Tracker.Overlay.Config
{
	public abstract class ConfigBase
	{
		[XmlAttribute]
		public bool Enabled { get; set; } = true;

		[XmlAttribute]
		public double Opacity { get; set; } = 1;

		[XmlAttribute]
		public double Scaling { get; set; } = 1;
	}

	public class OverlayConfig : ConfigBase
	{
		[XmlElement("Scene")]
		public List<SceneConfig> Scenes { get; set; }
	}

	public class SceneConfig : ConfigBase
	{
		[XmlAttribute]
		public Scene Scene { get; set; }

		[XmlElement("Element")]
		public List<ElementConfig> Elements { get; set; }
	}

	public class ElementConfig : ConfigBase
	{
		[XmlAttribute]
		public string Name { get; set; }

		[XmlAttribute]
		public string Type { get; set; }

		[XmlAttribute]
		public double Top { get; set; }

		[XmlAttribute]
		public double Left { get; set; }

		[XmlAttribute]
		public ScreenRegion ScreenRegion { get; set; }

		[XmlAttribute]
		public bool HasInteractableChildren { get; set; }
	}

	public enum ScreenRegion
	{
		Center,
		Full
	}


}
