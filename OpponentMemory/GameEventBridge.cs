using System;
using System.Threading;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Enums;

namespace OpponentMemory
{
	internal static class GameEventBridge
	{
		private static readonly object RegistrationSync = new object();
		private static OpponentMemoryPlugin? _activePlugin;
		private static bool _registered;

		internal static void Activate(OpponentMemoryPlugin plugin)
		{
			lock(RegistrationSync)
			{
				if(!_registered)
				{
					GameEvents.OnEntityWillTakeDamage.Add(DispatchEntityWillTakeDamage);
					GameEvents.OnGameStart.Add(DispatchGameStart);
					GameEvents.OnTurnStart.Add(DispatchTurnStart);
					_registered = true;
				}
				Volatile.Write(ref _activePlugin, plugin);
			}
		}

		internal static void Deactivate(OpponentMemoryPlugin plugin)
			=> Interlocked.CompareExchange(ref _activePlugin, null, plugin);

		private static void DispatchEntityWillTakeDamage(PredamageInfo info)
		{
			var plugin = Volatile.Read(ref _activePlugin);
			if(plugin == null)
				return;
			try
			{
				plugin.HandleEntityWillTakeDamage(info);
			}
			catch(Exception ex)
			{
				PluginLogger.Warn("Damage event handler failed: " + ex);
			}
		}

		private static void DispatchGameStart()
		{
			var plugin = Volatile.Read(ref _activePlugin);
			if(plugin == null)
				return;
			try
			{
				plugin.HandleGameStart();
			}
			catch(Exception ex)
			{
				PluginLogger.Warn("Game-start event handler failed: " + ex);
			}
		}

		private static void DispatchTurnStart(ActivePlayer player)
		{
			var plugin = Volatile.Read(ref _activePlugin);
			if(plugin == null)
				return;
			try
			{
				plugin.HandleTurnStart(player);
			}
			catch(Exception ex)
			{
				PluginLogger.Warn("Turn-start event handler failed: " + ex);
			}
		}
	}
}
