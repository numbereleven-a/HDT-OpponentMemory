using System;

namespace OpponentMemory
{
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
		{
			if(!IsPending)
				return false;
			if(!isSupported)
			{
				Suspend();
				return false;
			}
			if(!_supportedSinceUtc.HasValue)
				_supportedSinceUtc = nowUtc;
			if(!completionStateReady)
			{
				_stableSinceUtc = null;
				return nowUtc - _supportedSinceUtc.Value >= CompletionStateTimeout;
			}
			if(!_stableSinceUtc.HasValue)
			{
				_stableSinceUtc = nowUtc;
				return false;
			}

			var elapsed = nowUtc - _stableSinceUtc.Value;
			return resultStateReady
				? elapsed >= ResultDelay
				: elapsed >= MissingDataTimeout;
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
