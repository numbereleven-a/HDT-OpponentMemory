using System;
using System.Collections.Generic;
using System.Linq;

namespace OpponentMemory
{
	public sealed class LeaderboardReadiness
	{
		private static readonly TimeSpan PartialLobbyStabilityDelay = TimeSpan.FromSeconds(1);
		private bool _standardLobbyInitialized;
		private string? _candidateSignature;
		private DateTime _candidateSinceUtc;

		public bool IsReady(IReadOnlyList<LeaderboardPlayer> players, bool requireEightPlayers, DateTime nowUtc)
		{
			if(!IsValidPartialList(players))
			{
				ClearCandidate();
				return false;
			}
			if(requireEightPlayers)
			{
				if(!_standardLobbyInitialized)
					_standardLobbyInitialized = players.Count == 8;
				return _standardLobbyInitialized;
			}

			var signature = string.Join(";", players.OrderBy(player => player.LeaderboardPlace).Select(player => player.PlayerId + ":" + player.LeaderboardPlace));
			if(!string.Equals(signature, _candidateSignature, StringComparison.Ordinal))
			{
				_candidateSignature = signature;
				_candidateSinceUtc = nowUtc;
				return false;
			}
			return nowUtc - _candidateSinceUtc >= PartialLobbyStabilityDelay;
		}

		public void Reset()
		{
			_standardLobbyInitialized = false;
			ClearCandidate();
		}

		private static bool IsValidPartialList(IReadOnlyList<LeaderboardPlayer> players)
		{
			return players.Count > 0
			       && players.Any(player => player.IsLocalPlayer)
			       && players.All(player => player.LeaderboardPlace >= 1 && player.LeaderboardPlace <= 8)
			       && players.Select(player => player.LeaderboardPlace).Distinct().Count() == players.Count;
		}

		private void ClearCandidate()
		{
			_candidateSignature = null;
			_candidateSinceUtc = default;
		}
	}
}
