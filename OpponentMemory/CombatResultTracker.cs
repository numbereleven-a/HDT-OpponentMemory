using System;

namespace OpponentMemory
{
	public enum CombatOutcome
	{
		Unknown,
		Win,
		Loss,
		Draw
	}

	public readonly struct CompletedCombat
	{
		public CompletedCombat(CombatOutcome outcome, int? exactDamage)
		{
			Outcome = outcome;
			ExactDamage = exactDamage;
		}

		public CombatOutcome Outcome { get; }
		public int? ExactDamage { get; }
	}

	public sealed class CombatResultTracker
	{
		private readonly object _sync = new object();
		private int? _localPlayerId;
		private int? _opponentPlayerId;
		private int? _localDurabilityAtStart;
		private int? _opponentDurabilityAtStart;
		private int? _damagedPlayerId;
		private int? _recordedDamageAmount;
		private bool _opponentWasGhost;

		public static bool WasStateRestoredDuringCombat(
			bool restoredAtStart,
			bool restoredNow,
			long? clientHandleAtStart,
			long? currentClientHandle,
			int gameStartGenerationAtStart = 0,
			int currentGameStartGeneration = 0)
		{
			return gameStartGenerationAtStart != currentGameStartGeneration
				|| !restoredAtStart && restoredNow
				|| clientHandleAtStart.HasValue && currentClientHandle.HasValue && clientHandleAtStart != currentClientHandle;
		}

		public void StartCombat(int localPlayerId, int opponentPlayerId, int? localDurability, int? opponentDurability, bool opponentWasGhost)
		{
			if(localPlayerId <= 0 || opponentPlayerId <= 0 || localPlayerId == opponentPlayerId)
				return;
			lock(_sync)
			{
				_localPlayerId = localPlayerId;
				_opponentPlayerId = opponentPlayerId;
				_localDurabilityAtStart = localDurability;
				_opponentDurabilityAtStart = opponentDurability;
				_damagedPlayerId = null;
				_recordedDamageAmount = null;
				_opponentWasGhost = opponentWasGhost;
			}
		}

		public void RecordHeroDamage(int damagedPlayerId, int? exactDamage = null)
		{
			lock(_sync)
			{
				if(damagedPlayerId == _localPlayerId || damagedPlayerId == _opponentPlayerId)
				{
					_damagedPlayerId = damagedPlayerId;
					_recordedDamageAmount = exactDamage is > 0 ? exactDamage : null;
				}
			}
		}

		public void DiscardRecordedDamage()
		{
			lock(_sync)
			{
				_damagedPlayerId = null;
				_recordedDamageAmount = null;
			}
		}

		public CombatOutcome CompleteCombat(int opponentPlayerId, int? localDurability, int? opponentDurability, bool localPlayerWonLastCombat, bool assumeDrawIfNoDamage)
			=> CompleteCombatWithDamage(opponentPlayerId, localDurability, opponentDurability, localPlayerWonLastCombat, assumeDrawIfNoDamage).Outcome;

		public CompletedCombat CompleteCombatWithDamage(int opponentPlayerId, int? localDurability, int? opponentDurability, bool localPlayerWonLastCombat, bool assumeDrawIfNoDamage)
		{
			lock(_sync)
			{
				if(_localPlayerId is not > 0 || _opponentPlayerId != opponentPlayerId)
					return new CompletedCombat(CombatOutcome.Unknown, null);

				var localDamage = GetDurabilityLoss(_localDurabilityAtStart, localDurability);
				var opponentDamage = _opponentWasGhost ? 0 : GetDurabilityLoss(_opponentDurabilityAtStart, opponentDurability);
				CombatOutcome outcome;
				if(localDamage > 0 && opponentDamage == 0)
					outcome = CombatOutcome.Loss;
				else if(opponentDamage > 0 && localDamage == 0)
					outcome = CombatOutcome.Win;
				else if(_damagedPlayerId == _localPlayerId)
					outcome = CombatOutcome.Loss;
				else if(_damagedPlayerId == _opponentPlayerId)
					outcome = CombatOutcome.Win;
				else if(localPlayerWonLastCombat)
					outcome = CombatOutcome.Win;
				else
					outcome = assumeDrawIfNoDamage ? CombatOutcome.Draw : CombatOutcome.Unknown;

				int? exactDamage = null;
				if(outcome == CombatOutcome.Draw)
					exactDamage = 0;
				else if(outcome == CombatOutcome.Win && _damagedPlayerId == _opponentPlayerId)
					exactDamage = _recordedDamageAmount ?? PositiveValue(opponentDamage);
				else if(outcome == CombatOutcome.Win)
					exactDamage = PositiveValue(opponentDamage);
				else if(outcome == CombatOutcome.Loss && _damagedPlayerId == _localPlayerId)
					exactDamage = _recordedDamageAmount ?? PositiveValue(localDamage);
				else if(outcome == CombatOutcome.Loss)
					exactDamage = PositiveValue(localDamage);

				ClearActiveCombat();
				return new CompletedCombat(outcome, exactDamage);
			}
		}

		public void Reset()
		{
			lock(_sync)
				ClearActiveCombat();
		}

		private static int GetDurabilityLoss(int? before, int? after)
		{
			if(!before.HasValue || !after.HasValue)
				return 0;
			return Math.Max(0, before.Value - after.Value);
		}

		private static int? PositiveValue(int value) => value > 0 ? value : (int?)null;

		private void ClearActiveCombat()
		{
			_localPlayerId = null;
			_opponentPlayerId = null;
			_localDurabilityAtStart = null;
			_opponentDurabilityAtStart = null;
			_damagedPlayerId = null;
			_recordedDamageAmount = null;
			_opponentWasGhost = false;
		}
	}
}
