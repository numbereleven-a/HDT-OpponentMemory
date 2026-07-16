using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;

namespace OpponentMemory
{
	public sealed class BattlegroundsPlayerResolver
	{
		public bool IsSupportedSoloMatch()
		{
			var game = Core.Game;
			return game != null && !game.IsInMenu && game.IsBattlegroundsSoloMatch && !game.IsBattlegroundsDuosMatch;
		}

		public int GetRound() => Core.Game?.GetTurnNumber() ?? 0;
		public int GetLocalPlayerId()
		{
			var game = Core.Game;
			if(game == null)
				return 0;
			var playerId = game.PlayerEntity?.GetTag(GameTag.PLAYER_ID) ?? 0;
			return playerId > 0 ? playerId : game.Player.Id;
		}

		public int? GetHeroDurability(int playerId)
		{
			var hero = FindHero(playerId);
			return hero == null ? (int?)null : hero.Health + hero.GetTag(GameTag.ARMOR);
		}
		public bool RequiresEightPlayerLeaderboard() => Core.Game?.CurrentGameType == GameType.GT_BATTLEGROUNDS;
		public GameHandleSnapshot GetGameHandles()
		{
			var metadataHandle = Core.Game?.MetaData.ServerInfo?.GameHandle ?? 0;
			var statsHandle = Core.Game?.CurrentGameStats?.ServerInfo?.GameHandle ?? 0;
			return new GameHandleSnapshot(
				metadataHandle > 0 ? metadataHandle : (uint?)null,
				statsHandle > 0 ? statsHandle : (uint?)null);
		}

		public bool HasDefinitiveMatchResult()
		{
			var stats = Core.Game?.CurrentGameStats;
			return MatchEndPolicy.ShouldClearState(stats != null, stats != null && stats.Result != GameResult.None);
		}
		public int? GetScheduledOpponentPlayerId()
		{
			var entity = Core.Game?.PlayerEntity;
			var id = entity?.GetTag(GameTag.NEXT_OPPONENT_PLAYER_ID) ?? 0;
			return id > 0 ? id : null;
		}

		public bool IsGhostOpponent(int playerId)
		{
			var hero = FindHero(playerId);
			return hero != null && hero.Health <= 0;
		}

		public IReadOnlyList<LeaderboardPlayer> GetLeaderboardPlayers()
		{
			var game = Core.Game;
			if(game == null)
				return Array.Empty<LeaderboardPlayer>();
			var localPlayerId = GetLocalPlayerId();
			return game.Entities.Values
				.Where(entity => entity.IsHero)
				.Select(entity => new { Entity = entity, PlayerId = entity.GetTag(GameTag.PLAYER_ID), Place = entity.GetTag(GameTag.PLAYER_LEADERBOARD_PLACE) })
				.Where(x => x.PlayerId > 0 && x.Place > 0 && x.Place <= 8)
				.GroupBy(x => x.PlayerId)
				.Select(group => group.First())
				.OrderBy(x => x.Place)
				.Select(x => new LeaderboardPlayer(x.PlayerId, x.Place, x.PlayerId == localPlayerId, x.Entity.Health <= 0))
				.ToArray();
		}

		private static Entity? FindHero(int playerId)
		{
			return Core.Game?.Entities.Values
				.Where(entity => entity.IsHero && entity.GetTag(GameTag.PLAYER_ID) == playerId)
				.OrderByDescending(entity => entity.GetTag(GameTag.PLAYER_LEADERBOARD_PLACE) > 0)
				.FirstOrDefault();
		}
	}

	public sealed class LeaderboardPlayer
	{
		public LeaderboardPlayer(int playerId, int leaderboardPlace, bool isLocalPlayer, bool isDead)
		{
			PlayerId = playerId;
			LeaderboardPlace = leaderboardPlace;
			IsLocalPlayer = isLocalPlayer;
			IsDead = isDead;
		}
		public int PlayerId { get; }
		public int LeaderboardPlace { get; }
		public bool IsLocalPlayer { get; }
		public bool IsDead { get; }
	}
}
