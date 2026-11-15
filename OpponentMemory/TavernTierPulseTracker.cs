using System;
using System.Collections.Generic;

namespace OpponentMemory
{
	public sealed class TavernTierPulseTracker
	{
		private readonly Dictionary<int, int> _lastKnownTiers = new Dictionary<int, int>();
		private readonly Dictionary<int, DateTime> _pulseDeadlinesUtc = new Dictionary<int, DateTime>();
		private readonly List<int> _expiredIds = new List<int>();
		private int _lastRound;
		private bool _wasCombat;
		private bool _hasObservedPhase;

		public bool Update(
			IReadOnlyList<LeaderboardPlayer> players,
			int round,
			bool inCombat,
			bool enabled,
			double durationSeconds,
			DateTime nowUtc)
		{
			var hadActivePulses = _pulseDeadlinesUtc.Count > 0;
			var stateChanged = false;

			if(_hasObservedPhase && (!_wasCombat && inCombat || round > 0 && _lastRound > 0 && round != _lastRound))
			{
				if(_pulseDeadlinesUtc.Count > 0)
				{
					_pulseDeadlinesUtc.Clear();
					stateChanged = true;
				}
			}

			if(round > 0)
				_lastRound = round;
			_wasCombat = inCombat;
			_hasObservedPhase = true;

			if(_pulseDeadlinesUtc.Count > 0)
			{
				_expiredIds.Clear();
				foreach(var pair in _pulseDeadlinesUtc)
				{
					if(nowUtc >= pair.Value)
						_expiredIds.Add(pair.Key);
				}
				if(_expiredIds.Count > 0)
				{
					foreach(var id in _expiredIds)
						_pulseDeadlinesUtc.Remove(id);
					stateChanged = true;
				}
			}

			var allowPulse = enabled && durationSeconds > 0 && !inCombat;
			if(!enabled || durationSeconds <= 0)
			{
				if(_pulseDeadlinesUtc.Count > 0)
				{
					_pulseDeadlinesUtc.Clear();
					stateChanged = true;
				}
			}

			foreach(var player in players)
			{
				if(player.IsLocalPlayer || !player.TavernTier.HasValue)
					continue;
				var tier = player.TavernTier.Value;
				var hadPrevious = _lastKnownTiers.TryGetValue(player.PlayerId, out var previousTier);
				_lastKnownTiers[player.PlayerId] = tier;

				if(player.IsDead)
				{
					if(_pulseDeadlinesUtc.Remove(player.PlayerId))
						stateChanged = true;
					continue;
				}

				if(allowPulse && hadPrevious && tier != previousTier)
				{
					var wasPulsing = _pulseDeadlinesUtc.ContainsKey(player.PlayerId);
					_pulseDeadlinesUtc[player.PlayerId] = nowUtc + TimeSpan.FromSeconds(durationSeconds);
					if(!wasPulsing)
						stateChanged = true;
				}
			}

			return stateChanged || hadActivePulses != (_pulseDeadlinesUtc.Count > 0);
		}

		public bool IsPulsing(int playerId, DateTime nowUtc)
			=> _pulseDeadlinesUtc.TryGetValue(playerId, out var deadlineUtc) && nowUtc < deadlineUtc;

		public void Reset()
		{
			_lastKnownTiers.Clear();
			_pulseDeadlinesUtc.Clear();
			_expiredIds.Clear();
			_lastRound = 0;
			_wasCombat = false;
			_hasObservedPhase = false;
		}
	}
}
