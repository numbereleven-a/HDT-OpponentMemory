using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Plugins;

namespace OpponentMemory
{
	public sealed class OpponentMemoryPlugin : IPlugin
	{
		public const string DisplayVersion = "1.1";
		private readonly EncounterTracker _tracker = new EncounterTracker();
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

		public string Name => "Opponent Memory";
		public string Description => "Tracks how many times you have faced each opponent in Hearthstone Battlegrounds.";
		public string ButtonText => "Settings";
		public string Author => "";
		public Version Version => new Version(1, 1, 0, 0);
		public MenuItem MenuItem => _menuItem ??= BuildMenu();

		public void OnLoad()
		{
			_settings = OpponentMemorySettings.Load();
			_loaded = true;
			InvokeUi(_overlay.Attach);
			PluginLogger.Info("Loaded.");
		}

		public void OnUnload()
		{
			_loaded = false;
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
							CompleteActiveCombat();
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
				var scheduledIsGhost = scheduled is > 0 && _resolver.IsGhostOpponent(scheduled.Value);
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
						_tracker.Schedule(round, scheduled, scheduledIsGhost);
						_tracker.StartCombat(round);
					}
				}
				else
					_tracker.Schedule(round, scheduled, scheduledIsGhost);
				_wasCombat = combat;
				if(_forceOverlayRefresh || _overlayRefresh.ElapsedMilliseconds >= OverlayRefreshIntervalMs)
				{
					_forceOverlayRefresh = false;
					_overlayRefresh.Restart();
					var players = _resolver.GetLeaderboardPlayers();
					if(_leaderboardReadiness.IsReady(players, _resolver.RequiresEightPlayerLeaderboard(), DateTime.UtcNow))
						InvokeUi(() => _overlay.Update(players, _tracker, _settings, scheduled));
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

		private void CompleteActiveCombat()
		{
			if(_tracker.ActiveCombatOpponentPlayerId is not > 0)
				return;
			var countEncounter = _settings.CountGhostEncounters || !_tracker.ActiveCombatWasGhost;
			_tracker.CompleteCombat(countEncounter);
			_forceOverlayRefresh = true;
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
			_hasMatchState = true;
			_gameHandle = gameHandle;
			_sawMenu = false;
			_wasCombat = null;
			_wasSupported = false;
			_leaderboardReadiness.Reset();
			_forceOverlayRefresh = true;
			PluginLogger.Info("New match state initialized.");
		}

		private void ResetMatchState()
		{
			_tracker.Reset();
			_wasCombat = null;
			_wasSupported = false;
			_hasMatchState = false;
			_sawMenu = false;
			_gameHandle = null;
			_leaderboardReadiness.Reset();
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
