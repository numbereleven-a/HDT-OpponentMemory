using System;

namespace OpponentMemory
{
	public enum CombatCompletionDecision
	{
		Wait,
		Finalize,
		Abandon
	}

	public sealed class CombatCompletionGate
	{
		private static readonly TimeSpan ResultDelay = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan MissingDataTimeout = TimeSpan.FromSeconds(3);
		private static readonly TimeSpan CompletionStateTimeout = TimeSpan.FromSeconds(10);
		private DateTime? _supportedSinceUtc;
		private DateTime? _stableSinceUtc;

		public bool IsPending { get; private set; }
		public bool WasInterrupted { get; private set; }

		public void Begin(bool interrupted)
		{
			if(!IsPending)
			{
				IsPending = true;
				_supportedSinceUtc = null;
			}
			WasInterrupted |= interrupted;
		}

		public void MarkInterrupted()
		{
			WasInterrupted = true;
		}

		public void Suspend()
		{
			_supportedSinceUtc = null;
			_stableSinceUtc = null;
		}

		public bool CanFinalize(DateTime nowUtc, bool isSupported, bool completionStateReady, bool resultStateReady)
			=> Evaluate(nowUtc, isSupported, completionStateReady, resultStateReady) == CombatCompletionDecision.Finalize;

		public CombatCompletionDecision Evaluate(DateTime nowUtc, bool isSupported, bool completionStateReady, bool resultStateReady)
		{
			if(!IsPending)
				return CombatCompletionDecision.Wait;
			if(!isSupported)
			{
				Suspend();
				return CombatCompletionDecision.Wait;
			}
			if(!_supportedSinceUtc.HasValue)
				_supportedSinceUtc = nowUtc;
			if(!completionStateReady)
			{
				_stableSinceUtc = null;
				return nowUtc - _supportedSinceUtc.Value >= CompletionStateTimeout
					? CombatCompletionDecision.Abandon
					: CombatCompletionDecision.Wait;
			}
			if(!_stableSinceUtc.HasValue)
			{
				_stableSinceUtc = nowUtc;
				return CombatCompletionDecision.Wait;
			}

			var elapsed = nowUtc - _stableSinceUtc.Value;
			var ready = resultStateReady
				? elapsed >= ResultDelay
				: elapsed >= MissingDataTimeout;
			return ready ? CombatCompletionDecision.Finalize : CombatCompletionDecision.Wait;
		}

		public void Reset()
		{
			IsPending = false;
			WasInterrupted = false;
			_supportedSinceUtc = null;
			_stableSinceUtc = null;
		}
	}
}
