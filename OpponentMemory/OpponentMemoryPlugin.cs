using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Plugins;
using GameEvents = Hearthstone_Deck_Tracker.API.GameEvents;
using PredamageInfo = Hearthstone_Deck_Tracker.API.PredamageInfo;

namespace OpponentMemory
{
	public sealed class OpponentMemoryPlugin : IPlugin
	{
		public const string DisplayVersion = "1.5";
		private readonly EncounterTracker _tracker = new EncounterTracker();
		private readonly CombatResultTracker _combatResultTracker = new CombatResultTracker();
		private readonly CombatCompletionGate _combatCompletionGate = new CombatCompletionGate();
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
		private int? _lastCombatDamage;
		private int _eventGeneration;
		private bool _restoredStateAtCombatStart;
		private long? _clientHandleAtCombatStart;
		private int _gameStartGeneration;
		private int _gameStartGenerationAtCombatStart;
		private int _turnStartCompletionRequested;
		private int _playerTurnStartGeneration;
		private int _playerTurnStartGenerationAtCombatStart;
		private int _localPlayerIdAtCombatStart;

		public string Name => "Opponent Memory";
		public string Description =>
			"Tracks how many times you have faced each opponent in Hearthstone Battlegrounds.\n\n" +
			"GitHub: https://github.com/numbereleven-a/HDT-OpponentMemory";
		public string ButtonText => "Settings";
		public string Author => "numbereleven-a";
		public Version Version => new Version(1, 5);
		public MenuItem MenuItem => _menuItem ??= BuildMenu();

		public void OnLoad()
		{
			_settings = OpponentMemorySettings.Load();
			_loaded = true;
			RegisterCombatResultEventHandlers();
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
					if(_combatCompletionGate.IsPending)
						_combatCompletionGate.Suspend();
					if(Core.Game?.IsInMenu == true)
					{
						if(_tracker.ActiveCombatOpponentPlayerId is > 0)
							_combatCompletionGate.MarkInterrupted();
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
				if(Interlocked.Exchange(ref _turnStartCompletionRequested, 0) != 0 && _tracker.ActiveCombatOpponentPlayerId is > 0)
					BeginActiveCombatCompletion(false);
				if(_wasCombat == true && !combat)
					BeginActiveCombatCompletion(false);
				if(_combatCompletionGate.IsPending)
					TryFinalizeActiveCombat();
				if(!_combatCompletionGate.IsPending && combat)
				{
					if(_tracker.ActiveCombatOpponentPlayerId == null)
					{
						var scheduledIsGhost = GetScheduledGhostStatus(round, scheduled, true);
						_tracker.Schedule(round, scheduled, scheduledIsGhost);
						if(_tracker.StartCombat(round) && _tracker.ActiveCombatOpponentPlayerId is > 0)
						{
							var localPlayerId = _resolver.GetLocalPlayerId();
							var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
							_combatCompletionGate.Reset();
							_localPlayerIdAtCombatStart = localPlayerId;
							_combatResultTracker.StartCombat(
								localPlayerId,
								opponentPlayerId,
								_resolver.GetHeroDurability(localPlayerId),
								_resolver.GetHeroDurability(opponentPlayerId),
								_tracker.ActiveCombatWasGhost);
							_restoredStateAtCombatStart = _resolver.HasRestoredGameState();
							_clientHandleAtCombatStart = _resolver.GetClientHandle();
							_gameStartGenerationAtCombatStart = Volatile.Read(ref _gameStartGeneration);
							_playerTurnStartGenerationAtCombatStart = Volatile.Read(ref _playerTurnStartGeneration);
						}
					}
				}
				else if(!_combatCompletionGate.IsPending)
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
						InvokeUi(() => _overlay.Update(players, _tracker, _lastCombatOutcome, _lastCombatDamage, _settings, scheduled));
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

		private void BeginActiveCombatCompletion(bool interrupted)
		{
			if(_tracker.ActiveCombatOpponentPlayerId is > 0)
				_combatCompletionGate.Begin(interrupted);
		}

		private bool TryFinalizeActiveCombat()
		{
			if(_tracker.ActiveCombatOpponentPlayerId is not > 0)
			{
				_combatCompletionGate.Reset();
				return false;
			}
			var localPlayerId = _localPlayerIdAtCombatStart;
			var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
			var currentClientHandle = _resolver.GetClientHandle();
			var stateRestoredDuringCombat = CombatResultTracker.WasStateRestoredDuringCombat(
				_restoredStateAtCombatStart,
				_resolver.HasRestoredGameState(),
				_clientHandleAtCombatStart,
				currentClientHandle,
				_gameStartGenerationAtCombatStart,
				Volatile.Read(ref _gameStartGeneration));
			if(stateRestoredDuringCombat)
				_combatCompletionGate.MarkInterrupted();
			var localDurability = _resolver.GetHeroDurability(localPlayerId);
			var opponentDurability = _resolver.GetHeroDurability(opponentPlayerId);
			var resultStateReady = localDurability.HasValue && (_tracker.ActiveCombatWasGhost || opponentDurability.HasValue);
			var completionStateReady = !_combatCompletionGate.WasInterrupted
				|| _resolver.HasRestoredGameState()
				|| _playerTurnStartGenerationAtCombatStart != Volatile.Read(ref _playerTurnStartGeneration);
			if(!_combatCompletionGate.CanFinalize(DateTime.UtcNow, true, completionStateReady, resultStateReady))
				return false;
			if(_combatCompletionGate.WasInterrupted)
				_combatResultTracker.DiscardRecordedDamage();
			var completedCombat = _combatResultTracker.CompleteCombatWithDamage(
				opponentPlayerId,
				localDurability,
				opponentDurability,
				_combatCompletionGate.WasInterrupted && _resolver.DidPlayerWinLastCombat(localPlayerId),
				completionStateReady && resultStateReady);
			var countEncounter = _settings.CountGhostEncounters || !_tracker.ActiveCombatWasGhost;
			if(_tracker.CompleteCombat(countEncounter))
			{
				_lastCombatOutcome = completedCombat.Outcome;
				_lastCombatDamage = completedCombat.ExactDamage;
			}
			_combatResultTracker.Reset();
			_combatCompletionGate.Reset();
			_forceOverlayRefresh = true;
			_restoredStateAtCombatStart = false;
			_clientHandleAtCombatStart = null;
			_gameStartGenerationAtCombatStart = Volatile.Read(ref _gameStartGeneration);
			_playerTurnStartGenerationAtCombatStart = Volatile.Read(ref _playerTurnStartGeneration);
			_localPlayerIdAtCombatStart = 0;
			return true;
		}

		private void RegisterCombatResultEventHandlers()
		{
			var generation = Interlocked.Increment(ref _eventGeneration);
			var weakPlugin = new WeakReference<OpponentMemoryPlugin>(this);
			GameEvents.OnEntityWillTakeDamage.Add(info =>
			{
				if(weakPlugin.TryGetTarget(out var plugin))
					plugin.HandleEntityWillTakeDamage(info, generation);
			});
			GameEvents.OnGameStart.Add(() =>
			{
				if(weakPlugin.TryGetTarget(out var plugin))
					plugin.HandleGameStart(generation);
			});
			GameEvents.OnTurnStart.Add(player =>
			{
				if(weakPlugin.TryGetTarget(out var plugin))
					plugin.HandleTurnStart(player, generation);
			});
		}

		private void HandleGameStart(int generation)
		{
			if(_loaded && Volatile.Read(ref _eventGeneration) == generation)
				Interlocked.Increment(ref _gameStartGeneration);
		}

		private void HandleTurnStart(ActivePlayer player, int generation)
		{
			if(_loaded && Volatile.Read(ref _eventGeneration) == generation && player == ActivePlayer.Player)
			{
				Interlocked.Increment(ref _playerTurnStartGeneration);
				Interlocked.Exchange(ref _turnStartCompletionRequested, 1);
			}
		}

		private void HandleEntityWillTakeDamage(PredamageInfo info, int generation)
		{
			if(!_loaded || Volatile.Read(ref _eventGeneration) != generation || info?.Entity == null || !info.Entity.IsHero)
				return;
			var game = Core.Game;
			if(game == null || !game.IsBattlegroundsSoloMatch || game.IsBattlegroundsDuosMatch || (!game.IsBattlegroundsCombatPhase && !_combatCompletionGate.IsPending) || game.Player.Id <= 0 || _tracker.ActiveCombatOpponentPlayerId is not > 0)
				return;
			var localPlayerId = _resolver.GetLocalPlayerId();
			var opponentPlayerId = _tracker.ActiveCombatOpponentPlayerId.Value;
			var entityPlayerId = info.Entity.GetTag(GameTag.PLAYER_ID);
			var opponentControllerId = game.Opponent.Id;
			if(entityPlayerId == localPlayerId || info.Entity.IsControlledBy(game.Player.Id))
				_combatResultTracker.RecordHeroDamage(localPlayerId, info.Value);
			else if(entityPlayerId == opponentPlayerId || opponentControllerId > 0 && (entityPlayerId == opponentControllerId || info.Entity.IsControlledBy(opponentControllerId)))
				_combatResultTracker.RecordHeroDamage(opponentPlayerId, info.Value);
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
			_combatCompletionGate.Reset();
			_lastCombatOutcome = CombatOutcome.Unknown;
			_lastCombatDamage = null;
			_restoredStateAtCombatStart = false;
			_clientHandleAtCombatStart = null;
			_gameStartGenerationAtCombatStart = Volatile.Read(ref _gameStartGeneration);
			_playerTurnStartGenerationAtCombatStart = Volatile.Read(ref _playerTurnStartGeneration);
			Interlocked.Exchange(ref _turnStartCompletionRequested, 0);
			_localPlayerIdAtCombatStart = 0;
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
			_combatCompletionGate.Reset();
			_lastCombatOutcome = CombatOutcome.Unknown;
			_lastCombatDamage = null;
			_restoredStateAtCombatStart = false;
			_clientHandleAtCombatStart = null;
			_gameStartGenerationAtCombatStart = Volatile.Read(ref _gameStartGeneration);
			_playerTurnStartGenerationAtCombatStart = Volatile.Read(ref _playerTurnStartGeneration);
			Interlocked.Exchange(ref _turnStartCompletionRequested, 0);
			_localPlayerIdAtCombatStart = 0;
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
