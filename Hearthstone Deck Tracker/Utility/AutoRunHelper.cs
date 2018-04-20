using System.Windows;
using HearthSim.Util;

namespace Hearthstone_Deck_Tracker.Utility
{
	internal static class AutoRunHelper
	{
		private const string KeyName = "Hearthstone Deck Tracker";
		public static string ExecutablePath { get; set; } = Application.ResourceAssembly.Location;
		public static string Args { get; set; }

		public static void Set()
		{
			RegistryHelper.SetAutoRun(KeyName, ExecutablePath, Args);
		}

		public static void Delete()
		{
			RegistryHelper.DeleteRunKey(KeyName);
		}
	}
}
