using System;

namespace OpponentMemory
{
	public readonly struct GameHandleSnapshot
	{
		public GameHandleSnapshot(uint? metadataHandle, uint? statsHandle)
		{
			MetadataHandle = metadataHandle;
			StatsHandle = statsHandle;
		}

		public uint? MetadataHandle { get; }
		public uint? StatsHandle { get; }
		public bool HasConflict => MetadataHandle.HasValue && StatsHandle.HasValue && MetadataHandle.Value != StatsHandle.Value;
		public uint? AvailableHandle => MetadataHandle ?? StatsHandle;
		public bool Contains(uint handle) => MetadataHandle == handle || StatsHandle == handle;
	}

	public enum MatchIdentityAction
	{
		Preserve,
		StartNew,
		Wait
	}

	public readonly struct MatchIdentityDecision
	{
		public MatchIdentityDecision(MatchIdentityAction action, uint? gameHandle)
		{
			Action = action;
			GameHandle = gameHandle;
		}

		public MatchIdentityAction Action { get; }
		public uint? GameHandle { get; }
	}

	public static class MatchIdentityResolver
	{
		public static MatchIdentityDecision Evaluate(uint? storedHandle, bool afterMenu, GameHandleSnapshot snapshot)
		{
			if(snapshot.HasConflict)
			{
				// HDT can retain the previous metadata handle while the current game
				// statistics already identify the new match.
				if(afterMenu
				   && storedHandle.HasValue
				   && snapshot.MetadataHandle == storedHandle
				   && snapshot.StatsHandle.HasValue
				   && snapshot.StatsHandle != storedHandle)
					return new MatchIdentityDecision(MatchIdentityAction.StartNew, snapshot.StatsHandle);

				if(!afterMenu && storedHandle.HasValue && snapshot.Contains(storedHandle.Value))
					return new MatchIdentityDecision(MatchIdentityAction.Preserve, storedHandle);

				return new MatchIdentityDecision(MatchIdentityAction.Wait, null);
			}

			var currentHandle = snapshot.AvailableHandle;
			if(afterMenu)
			{
				if(!currentHandle.HasValue)
					return new MatchIdentityDecision(MatchIdentityAction.Wait, null);
				if(storedHandle.HasValue && currentHandle.Value == storedHandle.Value)
					return new MatchIdentityDecision(MatchIdentityAction.Preserve, currentHandle);
				return new MatchIdentityDecision(MatchIdentityAction.StartNew, currentHandle);
			}

			if(currentHandle.HasValue && storedHandle.HasValue && currentHandle.Value != storedHandle.Value)
				return new MatchIdentityDecision(MatchIdentityAction.StartNew, currentHandle);

			return new MatchIdentityDecision(MatchIdentityAction.Preserve, currentHandle ?? storedHandle);
		}
	}

	public sealed class MatchIdentityRecovery
	{
		private static readonly TimeSpan StableStateDelay = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan ReconnectDetectionTimeout = TimeSpan.FromSeconds(12);
		private DateTime? _waitingSinceUtc;

		public MatchIdentityDecision Evaluate(
			uint? storedHandle,
			bool afterMenu,
			GameHandleSnapshot snapshot,
			DateTime nowUtc,
			int matchGameStartGeneration,
			int currentGameStartGeneration,
			bool restoredGameState)
		{
			var decision = MatchIdentityResolver.Evaluate(storedHandle, afterMenu, snapshot);
			var gameStartChanged = currentGameStartGeneration != matchGameStartGeneration;
			var identityComparisonUnavailable = !storedHandle.HasValue || !snapshot.AvailableHandle.HasValue;
			var needsRecovery = !snapshot.HasConflict
				&& identityComparisonUnavailable
				&& (afterMenu || gameStartChanged);
			if(!needsRecovery)
			{
				Reset();
				return decision;
			}

			var action = EvaluateMissingHandle(
				nowUtc,
				matchGameStartGeneration,
				currentGameStartGeneration,
				restoredGameState);
			var recoveredHandle = action == MatchIdentityAction.StartNew
				? snapshot.AvailableHandle
				: snapshot.AvailableHandle ?? storedHandle;
			return new MatchIdentityDecision(action, recoveredHandle);
		}

		public void Reset() => _waitingSinceUtc = null;

		private MatchIdentityAction EvaluateMissingHandle(
			DateTime nowUtc,
			int matchGameStartGeneration,
			int currentGameStartGeneration,
			bool restoredGameState)
		{
			if(!_waitingSinceUtc.HasValue)
			{
				_waitingSinceUtc = nowUtc;
				return MatchIdentityAction.Wait;
			}

			if(restoredGameState)
				return MatchIdentityAction.Preserve;

			var elapsed = nowUtc - _waitingSinceUtc.Value;
			if(matchGameStartGeneration == currentGameStartGeneration)
				return elapsed >= StableStateDelay ? MatchIdentityAction.Preserve : MatchIdentityAction.Wait;

			return elapsed >= ReconnectDetectionTimeout ? MatchIdentityAction.StartNew : MatchIdentityAction.Wait;
		}
	}

	public static class MatchEndPolicy
	{
		public static bool ShouldClearState(bool hasGameStats, bool hasDefinitiveResult)
			=> hasGameStats && hasDefinitiveResult;
	}
}
