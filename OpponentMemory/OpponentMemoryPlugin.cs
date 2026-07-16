using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Plugins;
using GameEvents = Hearthstone_Deck_Tracker.API.GameEvents;
using PredamageInfo = Hearthstone_Deck_Tracker.API.PredamageInfo;

namespace OpponentMemory
{
	public sealed class OpponentMemoryPlugin : IPlugin
	{
		public const string DisplayVersion = "1.3";
		private readonly EncounterTracker _tracker = new EncounterTracker();
		private readonly CombatResultTracker _combatResultTracker = new CombatResultTracker();
		private readonly BattlegroundsPlayerResolver _resolver = new BattlegroundsPlayerResolver();
		private readonly OpponentMemoryOverlay _overlay = new OpponentMemoryOverlay();
		private readonly LeaderboardReadiness _leaderboardReadiness = new LeaderboardReadiness();
		private OpponentMemorySettings _settings = new OpponentMemorySettings();
		private SettingsWindow? _settingsWindow;
		private MenuItem? _menuItem;
		private MenuItem? _enabledMenuItem;
		private bool _loaded;
		private bool _wasSupported;
		private bool? _wasCombat;
		private bool _hasMatchState;
		private bool _sawMenu;
		private uint? _gameHandle;
		private DateTime _lastUpdateErrorLog;
		private const int OverlayRefreshIntervalMs = 300;
		private readonly Stopwatch _overlayRefresh = Stopwatch.StartNew();
		private bool _forceOverlayRefresh = true;
		private int? _ghostStatusRound;
		private int? _ghostStatusPlayerId;
		private bool _cachedScheduledIsGhost;
		private bool _overlayHiddenForBackground;
		private CombatOutcome _lastCombatOutcome;
		private int _eventGeneration;

		public string Name => "Opponent Memory";
		public string Description => "Tracks how many times you have faced each opponent in Hearthstone Battlegrounds.";
		public string ButtonText => "Settings";
		public string Author => "";
		public Version Version => new Version(1, 3);
		public MenuItem MenuItem => _menuItem ??= BuildMenu();

		public void OnLoad()
		{
			_settings = OpponentMemorySettings.Load();
			_loaded = true;
			RegisterCombatResultEventHandler();
			InvokeUi(_overlay.Attach);
			PluginLogger.Info("Loaded.");
		}

		public void OnUnload()
		{
			_loaded = false;
			Interlocked.Increment(ref _eventGeneration);
			ResetMatchState();
			InvokeUi(() => { _settingsWindow?.Close(); _settingsWindow = null; _overlay.Detach(); });
			_settings.Save();
			PluginLogger.Info("Unloaded.");
		}

		public void OnButtonPress() => ShowSettings();

		public void OnUpdate()
		{
			if(!_loaded)
				return;
			try
			{
				if(!_resolver.IsSupportedSoloMatch())
				{
					if(Core.Game?.IsInMenu == true)
					{
						if(_tracker.ActiveCombatOpponentPlayerId is > 0)
						{
							CompleteActiveCombat(false);
							_wasCombat = false;
						}
						if(_resolver.HasDefinitiveMatchResult())
							ResetMatchState();
						else
							_sawMenu = true;
					}
					_wasSupported = false;
					InvokeUi(_overlay.Hide);
					return;
				}
				var round = _resolver.GetRound();
				var gameHandles = _resolver.GetGameHandles();
				if(!PrepareMatchState(gameHandles))
				{
					InvokeUi(_overlay.Hide);
					return;
				}
				var combat = Core.Game.IsBattlegroundsCombatPhase;
				var scheduled = _resolver.GetScheduledOpponentPlayerId();
				if(!_wasSupported)
				{
					_wasSupported = true;
					if(!_wasCombat.HasValue)
						_wasCombat = combat;
				}
				if(_wasCombat == true && !combat)
					CompleteActiveCombat();
				if(combat)
				{
					if(_tracker.ActiveCombatOpponentPlayerId == null)
					{
						var scheduledIsGhost = GetScheduledGhostStatus(round, scheduled, true);
						_tracker.Schedule(round, scheduled, scheduledIsGhost);
						if(_tracker.StartCombat(round) && _tracker.ActiveCombatOpponentPlayerId is > 0)
						{
							var localPlayerId = _resolver.GetLocalPlayerId();
							var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
							_combatResultTracker.StartCombat(
								localPlayerId,
								opponentPlayerId,
								_resolver.GetHeroDurability(localPlayerId),
								_resolver.GetHeroDurability(opponentPlayerId),
								_tracker.ActiveCombatWasGhost);
						}
					}
				}
				else
					_tracker.Schedule(round, scheduled, GetScheduledGhostStatus(round, scheduled, false));
				_wasCombat = combat;
				if(!User32.IsHearthstoneInForeground())
				{
					if(!_overlayHiddenForBackground)
						InvokeUi(_overlay.Hide);
					_overlayHiddenForBackground = true;
					return;
				}
				if(_overlayHiddenForBackground)
				{
					_overlayHiddenForBackground = false;
					_forceOverlayRefresh = true;
				}
				if(_forceOverlayRefresh || _overlayRefresh.ElapsedMilliseconds >= OverlayRefreshIntervalMs)
				{
					_forceOverlayRefresh = false;
					_overlayRefresh.Restart();
					var players = _resolver.GetLeaderboardPlayers();
					if(_leaderboardReadiness.IsReady(players, _resolver.RequiresEightPlayerLeaderboard(), DateTime.UtcNow))
						InvokeUi(() => _overlay.Update(players, _tracker, _lastCombatOutcome, _settings, scheduled));
					else
						InvokeUi(_overlay.Hide);
				}
			}
			catch(Exception ex)
			{
				if(DateTime.UtcNow - _lastUpdateErrorLog > TimeSpan.FromSeconds(10))
				{
					_lastUpdateErrorLog = DateTime.UtcNow;
					PluginLogger.Warn("Update failed: " + ex);
				}
				InvokeUi(_overlay.Hide);
			}
		}

		private void CompleteActiveCombat(bool assumeDrawIfNoDamage = true)
		{
			if(_tracker.ActiveCombatOpponentPlayerId is not > 0)
				return;
			var localPlayerId = _resolver.GetLocalPlayerId();
			var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
			var outcome = _combatResultTracker.CompleteCombat(
				opponentPlayerId,
				_resolver.GetHeroDurability(localPlayerId),
				_resolver.GetHeroDurability(opponentPlayerId),
				assumeDrawIfNoDamage);
			var countEncounter = _settings.CountGhostEncounters || !_tracker.ActiveCombatWasGhost;
			if(_tracker.CompleteCombat(countEncounter))
				_lastCombatOutcome = outcome;
			_forceOverlayRefresh = true;
		}

		private void RegisterCombatResultEventHandler()
		{
			var generation = Interlocked.Increment(ref _eventGeneration);
			var weakPlugin = new WeakReference<OpponentMemoryPlugin>(this);
			GameEvents.OnEntityWillTakeDamage.Add(info =>
			{
				if(weakPlugin.TryGetTarget(out var plugin))
					plugin.HandleEntityWillTakeDamage(info, generation);
			});
		}

		private void HandleEntityWillTakeDamage(PredamageInfo info, int generation)
		{
			if(!_loaded || Volatile.Read(ref _eventGeneration) != generation || info?.Entity == null || !info.Entity.IsHero)
				return;
			var game = Core.Game;
			if(game == null || !game.IsBattlegroundsSoloMatch || game.IsBattlegroundsDuosMatch || !game.IsBattlegroundsCombatPhase || game.Player.Id <= 0 || _tracker.ActiveCombatOpponentPlayerId is not > 0)
				return;
			var localPlayerId = _resolver.GetLocalPlayerId();
			var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
			var entityPlayerId = info.Entity.GetTag(GameTag.PLAYER_ID);
			var opponentControllerId = game.Opponent.Id;
			if(entityPlayerId == localPlayerId || info.Entity.IsControlledBy(game.Player.Id))
				_combatResultTracker.RecordHeroDamage(localPlayerId);
			else if(entityPlayerId == opponentPlayerId || opponentControllerId > 0 && (entityPlayerId == opponentControllerId || info.Entity.IsControlledBy(opponentControllerId)))
				_combatResultTracker.RecordHeroDamage(opponentPlayerId);
		}

		private bool GetScheduledGhostStatus(int round, int? playerId, bool forceRefresh)
		{
			if(playerId is not > 0)
				return false;
			if(forceRefresh || _ghostStatusRound != round || _ghostStatusPlayerId != playerId)
			{
				var isGhost = _resolver.IsGhostOpponent(playerId.Value);
				_ghostStatusRound = round;
				_ghostStatusPlayerId = playerId;
				_cachedScheduledIsGhost = isGhost;
			}
			return _cachedScheduledIsGhost;
		}

		private void ResetGhostStatusCache()
		{
			_ghostStatusRound = null;
			_ghostStatusPlayerId = null;
			_cachedScheduledIsGhost = false;
			_overlayHiddenForBackground = false;
		}

		private bool PrepareMatchState(GameHandleSnapshot gameHandles)
		{
			if(!_hasMatchState)
			{
				if(gameHandles.HasConflict)
					return false;
				InitializeNewMatch(gameHandles.AvailableHandle);
				return true;
			}

			var decision = MatchIdentityResolver.Evaluate(_gameHandle, _sawMenu, gameHandles);
			if(decision.Action == MatchIdentityAction.Wait)
				return false;
			if(decision.Action == MatchIdentityAction.StartNew)
			{
				InitializeNewMatch(decision.GameHandle);
				return true;
			}

			_sawMenu = false;
			if(decision.GameHandle.HasValue)
				_gameHandle = decision.GameHandle;
			return true;
		}

		private void InitializeNewMatch(uint? gameHandle)
		{
			_tracker.Reset();
			_combatResultTracker.Reset();
			_lastCombatOutcome = CombatOutcome.Unknown;
			_hasMatchState = true;
			_gameHandle = gameHandle;
			_sawMenu = false;
			_wasCombat = null;
			_wasSupported = false;
			_leaderboardReadiness.Reset();
			ResetGhostStatusCache();
			_forceOverlayRefresh = true;
			PluginLogger.Info("New match state initialized.");
		}

		private void ResetMatchState()
		{
			_tracker.Reset();
			_combatResultTracker.Reset();
			_lastCombatOutcome = CombatOutcome.Unknown;
			_wasCombat = null;
			_wasSupported = false;
			_hasMatchState = false;
			_sawMenu = false;
			_gameHandle = null;
			_leaderboardReadiness.Reset();
			ResetGhostStatusCache();
		}

		private MenuItem BuildMenu()
		{
			var root = new MenuItem { Header = "Opponent Memory" };
			_enabledMenuItem = new MenuItem { Header = "Enabled", IsCheckable = true, IsChecked = _settings.Enabled };
			_enabledMenuItem.Click += (_, __) => { _settings.Enabled = _enabledMenuItem.IsChecked; _settings.Save(); _forceOverlayRefresh = true; };
			var settings = new MenuItem { Header = "Settings" }; settings.Click += (_, __) => ShowSettings();
			var reset = new MenuItem { Header = "Reset settings" }; reset.Click += (_, __) => { _settings.CopyFrom(OpponentMemorySettings.CreateDefaults()); _settings.Save(); SyncMenuState(); };
			root.Items.Add(_enabledMenuItem); root.Items.Add(settings); root.Items.Add(reset);
			return root;
		}

		private void ShowSettings() => InvokeUi(() =>
		{
			if(_settingsWindow != null) { _settingsWindow.Activate(); return; }
			_settingsWindow = new SettingsWindow(_settings, ApplySettings);
			_settingsWindow.Closed += (_, __) => _settingsWindow = null;
			_settingsWindow.Show();
		});

		private void ApplySettings()
		{
			SyncMenuState();
			_forceOverlayRefresh = true;
		}

		private void SyncMenuState()
		{
			if(_enabledMenuItem != null)
				_enabledMenuItem.IsChecked = _settings.Enabled;
		}

		private static void InvokeUi(Action action)
		{
			var dispatcher = Application.Current?.Dispatcher;
			if(dispatcher == null || dispatcher.CheckAccess()) action(); else dispatcher.BeginInvoke(action);
		}
	}
}
