using Hearthstone_Deck_Tracker.Utility.Logging;

namespace OpponentMemory
{
	internal static class PluginLogger
	{
		internal static void Info(string message) => Log.Info("[OpponentMemory] " + message);
		internal static void Warn(string message) => Log.Warn("[OpponentMemory] " + message);
	}
}
