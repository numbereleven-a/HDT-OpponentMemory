using System;
using System.Collections.Generic;
using OpponentMemory;

namespace OpponentMemory.Tests
{
	internal static class Program
	{
		private static int Main()
		{
			FirstCombatIsCounted();
			RepeatedCombatIsCounted();
			ScheduledOpponentIsNotCountedEarly();
			DuplicateRoundIsIgnored();
			AdjacentCombatRoundsDoNotCollide();
			LateOpponentIdIsCaptured();
			OldScheduledOpponentIsRejected();
			RoundZeroIsRejected();
			GhostSettingIsRespected();
			GhostStatusIsCapturedAtCombatStart();
			ResetClearsMatchState();
			RestoredStateDoesNotInventHistory();
			SettingsAreClamped();
			StandardLeaderboardWaitsForEightPlayers();
			PartialLeaderboardRequiresStability();
			Console.WriteLine("All Opponent Memory tests passed.");
			return 0;
		}

		private static void FirstCombatIsCounted()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(1, 2); tracker.StartCombat(1);
			True(tracker.CompleteCombat(true), "first combat completes"); Equal(1, tracker.GetCount(2), "first combat count"); Equal(2, tracker.LastCompletedOpponentPlayerId, "last opponent");
		}

		private static void RepeatedCombatIsCounted()
		{
			var tracker = new EncounterTracker();
			tracker.Schedule(1, 2); tracker.StartCombat(1); tracker.CompleteCombat(true);
			tracker.Schedule(2, 2); tracker.StartCombat(2); tracker.CompleteCombat(true);
			Equal(2, tracker.GetCount(2), "repeated encounter count");
		}

		private static void ScheduledOpponentIsNotCountedEarly()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(1, 3);
			Equal(0, tracker.GetCount(3), "scheduled opponent remains zero");
		}

		private static void DuplicateRoundIsIgnored()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(5, 4); tracker.StartCombat(5); tracker.CompleteCombat(true);
			tracker.Schedule(5, 4); tracker.StartCombat(5); False(tracker.CompleteCombat(true), "same round not counted twice"); Equal(1, tracker.GetCount(4), "same round count");
		}

		private static void AdjacentCombatRoundsDoNotCollide()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(4, 4); tracker.StartCombat(4); tracker.CompleteCombat(true);
			tracker.Schedule(5, 5); tracker.StartCombat(5); True(tracker.CompleteCombat(true), "next combat round is counted independently"); Equal(1, tracker.GetCount(5), "next opponent count");
		}

		private static void LateOpponentIdIsCaptured()
		{
			var tracker = new EncounterTracker();
			tracker.StartCombat(6);
			Equal<int?>(null, tracker.ActiveCombatOpponentPlayerId, "combat begins without an opponent id");
			tracker.Schedule(6, 9);
			tracker.StartCombat(6);
			Equal<int?>(9, tracker.ActiveCombatOpponentPlayerId, "late opponent id is captured");
		}

		private static void OldScheduledOpponentIsRejected()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(3, 8);
			False(tracker.StartCombat(4), "opponent scheduled for another round is rejected"); Equal<int?>(null, tracker.ActiveCombatOpponentPlayerId, "old opponent is not activated");
		}

		private static void RoundZeroIsRejected()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(0, 8);
			False(tracker.StartCombat(0), "round zero combat is rejected"); Equal<int?>(null, tracker.ActiveCombatRound, "round zero is not stored");
		}

		private static void GhostSettingIsRespected()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(1, 5); tracker.StartCombat(1); tracker.CompleteCombat(true); Equal(1, tracker.GetCount(5), "ghost counted when enabled");
			tracker.Reset(); tracker.Schedule(1, 5); tracker.StartCombat(1); False(tracker.CompleteCombat(false), "ghost ignored when disabled"); Equal(0, tracker.GetCount(5), "ignored ghost remains zero");
		}

		private static void GhostStatusIsCapturedAtCombatStart()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(3, 7, false); tracker.StartCombat(3);
			False(tracker.ActiveCombatWasGhost, "live opponent is not marked as a ghost");
			tracker.CompleteCombat(true); Equal(1, tracker.GetCount(7), "live opponent is counted after combat");
			tracker.Reset(); tracker.Schedule(4, 7, true); tracker.StartCombat(4);
			True(tracker.ActiveCombatWasGhost, "ghost status is captured at combat start");
		}

		private static void ResetClearsMatchState()
		{
			var tracker = new EncounterTracker(); tracker.Schedule(1, 6); tracker.StartCombat(1); tracker.CompleteCombat(true); tracker.Reset();
			Equal(0, tracker.GetCount(6), "reset clears counts"); Equal<int?>(null, tracker.LastCompletedOpponentPlayerId, "reset clears last opponent");
		}

		private static void RestoredStateDoesNotInventHistory()
		{
			var tracker = new EncounterTracker(); tracker.StartCombat(2); False(tracker.CompleteCombat(true), "combat without a scheduled opponent is ignored");
		}

		private static void SettingsAreClamped()
		{
			var settings = new OpponentMemorySettings { Scale = 9, TextOpacity = -1, FontSize = double.NaN }; settings.Normalize();
			Equal(1d, settings.Scale, "scale fallback"); Equal(100d, settings.TextOpacity, "opacity fallback"); Equal(22d, settings.FontSize, "font fallback"); True(settings.ShowEncounterCounts, "counts visible by default"); True(settings.HighlightLastOpponent, "highlight visible by default");
		}

		private static void StandardLeaderboardWaitsForEightPlayers()
		{
			var readiness = new LeaderboardReadiness();
			var partial = Players(4);
			False(readiness.IsReady(partial, true, DateTime.UtcNow), "standard lobby waits for eight players");
			True(readiness.IsReady(Players(8), true, DateTime.UtcNow), "complete standard lobby is ready");
			True(readiness.IsReady(partial, true, DateTime.UtcNow), "confirmed standard lobby tolerates temporary partial data");
		}

		private static void PartialLeaderboardRequiresStability()
		{
			var readiness = new LeaderboardReadiness();
			var now = DateTime.UtcNow;
			var players = Players(4);
			False(readiness.IsReady(players, false, now), "partial lobby starts stabilization");
			False(readiness.IsReady(players, false, now.AddMilliseconds(900)), "partial lobby is not ready too early");
			True(readiness.IsReady(players, false, now.AddSeconds(1)), "stable partial lobby becomes ready");
			var duplicatePlaces = new List<LeaderboardPlayer> { new LeaderboardPlayer(1, 1, true, false), new LeaderboardPlayer(2, 1, false, false) };
			False(readiness.IsReady(duplicatePlaces, false, now.AddSeconds(2)), "duplicate places are rejected");
		}

		private static IReadOnlyList<LeaderboardPlayer> Players(int count)
		{
			var players = new List<LeaderboardPlayer>();
			for(var index = 1; index <= count; index++)
				players.Add(new LeaderboardPlayer(index, index, index == 1, false));
			return players;
		}

		private static void True(bool value, string name) { if(!value) throw new InvalidOperationException(name); }
		private static void False(bool value, string name) { if(value) throw new InvalidOperationException(name); }
		private static void Equal<T>(T expected, T actual, string name) { if(!Equals(expected, actual)) throw new InvalidOperationException(name + ": expected " + expected + ", actual " + actual); }
	}
}
