using System.Collections.Generic;

namespace OpponentMemory
{
	public sealed class EncounterTracker
	{
		private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
		private bool _scheduledOpponentWasGhost;
		private int? _scheduledOpponentRound;
		public int? ScheduledOpponentPlayerId { get; private set; }
		public int? ActiveCombatOpponentPlayerId { get; private set; }
		public int? ActiveCombatRound { get; private set; }
		public bool ActiveCombatWasGhost { get; private set; }
		public int? LastCompletedOpponentPlayerId { get; private set; }
		public int? LastCountedRound { get; private set; }

		public void Schedule(int round, int? playerId, bool isGhost = false)
		{
			if(round <= 0)
				return;
			if(playerId is not > 0)
			{
				if(_scheduledOpponentRound != round)
					ClearScheduledOpponent();
				return;
			}
			ScheduledOpponentPlayerId = playerId;
			_scheduledOpponentRound = round;
			_scheduledOpponentWasGhost = isGhost;
		}

		public bool StartCombat(int round)
		{
			if(round <= 0 || ActiveCombatOpponentPlayerId is > 0 || ScheduledOpponentPlayerId is not > 0 || _scheduledOpponentRound != round)
				return false;
			ActiveCombatOpponentPlayerId = ScheduledOpponentPlayerId;
			ActiveCombatRound = round;
			ActiveCombatWasGhost = _scheduledOpponentWasGhost;
			ClearScheduledOpponent();
			return true;
		}

		private void ClearScheduledOpponent()
		{
			ScheduledOpponentPlayerId = null;
			_scheduledOpponentRound = null;
			_scheduledOpponentWasGhost = false;
		}

		public bool CompleteCombat(bool countEncounter)
		{
			var opponentId = ActiveCombatOpponentPlayerId;
			var combatRound = ActiveCombatRound;
			ActiveCombatOpponentPlayerId = null;
			ActiveCombatRound = null;
			ActiveCombatWasGhost = false;
			if(opponentId is not > 0 || combatRound is not > 0 || LastCountedRound == combatRound)
				return false;
			LastCountedRound = combatRound;
			if(!countEncounter)
				return false;
			_counts[opponentId.Value] = GetCount(opponentId.Value) + 1;
			LastCompletedOpponentPlayerId = opponentId.Value;
			return true;
		}

		public int GetCount(int playerId) => _counts.TryGetValue(playerId, out var count) ? count : 0;

		public void Reset()
		{
			_counts.Clear();
			ClearScheduledOpponent();
			ActiveCombatOpponentPlayerId = null;
			ActiveCombatRound = null;
			ActiveCombatWasGhost = false;
			LastCompletedOpponentPlayerId = null;
			LastCountedRound = null;
		}
	}
}
