#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Hearthstone_Deck_Tracker.Controls.Error;
using Hearthstone_Deck_Tracker.Utility.Extensions;

#endregion

namespace Hearthstone_Deck_Tracker.Utility.Logging
{
	[DebuggerStepThrough]
	public class Log
	{
		public static void Debug(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> HearthSim.Util.Logging.Log.Debug(msg, memberName, sourceFilePath);

		public static void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> HearthSim.Util.Logging.Log.Info(msg, memberName, sourceFilePath);

		public static void Warn(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> HearthSim.Util.Logging.Log.Warn(msg, memberName, sourceFilePath);

		public static void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> HearthSim.Util.Logging.Log.Error(msg, memberName, sourceFilePath);

		public static void Error(Exception ex, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
			=> HearthSim.Util.Logging.Log.Error(ex, memberName, sourceFilePath);
	}
}
