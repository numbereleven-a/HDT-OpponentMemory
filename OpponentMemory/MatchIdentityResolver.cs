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

	public static class MatchEndPolicy
	{
		public static bool ShouldClearState(bool hasGameStats, bool hasDefinitiveResult)
			=> hasGameStats && hasDefinitiveResult;
	}
}
