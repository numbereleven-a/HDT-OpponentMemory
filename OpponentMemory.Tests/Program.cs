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
			MatchIdentityUsesNewStatsHandleAfterMenu();
			MatchIdentityPreservesMatchingHandleAfterMenu();
			MatchIdentityWaitsForAmbiguousConflict();
			MatchIdentityPreservesStoredHandleDuringActiveConflict();
			MatchEndPolicyRequiresAConfirmedResult();
			CombatResultWinIsDetected();
			CombatResultLossIsDetected();
			CombatResultDrawIsDetected();
			ExactWinDamageIsCaptured();
			ExactLossDamageIsCaptured();
			DrawDamageIsZero();
			RestoredLossRecoversDamageFromDurability();
			RestoredWinRecoversDamageFromDurability();
			MissingDurabilityDoesNotInventDamage();
			InconsistentDamageEventUsesDurability();
			GhostExactDamageUsesTheDamageEvent();
			UncertainCombatResultRemainsUnknown();
			RestoredWinTagIsDetected();
			DurabilityChangeOverridesAnIncorrectEvent();
			GhostWinUsesTheDamageEvent();
			CombatResultResetClearsActiveCombat();
			RestoredStateTransitionIsScopedToTheAffectedCombat();
			GameStartGenerationDetectsAnEarlyStateRestore();
			LateDamageIsAcceptedBeforeFinalization();
			MenuInterruptionDoesNotEndTheCombat();
			InterruptedCombatWaitsForSupportedState();
			InterruptedCombatWaitsForCompletionState();
			InterruptedLossUsesRestoredDurability();
			RestoredDrawIgnoresReplayedDamage();
			MissingRestoredDataDoesNotBecomeADraw();
			InterruptedStateDoesNotLeakIntoTheNextCombat();
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
			var settings = new OpponentMemorySettings { Scale = 9, TextOpacity = -1, FontSize = double.NaN, TextStyle = (OverlayTextStyle)99 }; settings.Normalize();
			Equal(1d, settings.Scale, "scale fallback"); Equal(100d, settings.TextOpacity, "opacity fallback"); Equal(22d, settings.FontSize, "font fallback"); Equal(OverlayTextStyle.Outlined, settings.TextStyle, "outlined text is the default fallback"); True(settings.ShowEncounterCounts, "counts visible by default"); False(settings.ShowLastCombatDamage, "damage is hidden by default"); True(settings.HighlightLastOpponent, "highlight visible by default"); False(settings.ColorLastOpponentByCombatResult, "result coloring is opt-in"); Equal("Blue", settings.WinTextColor, "default win color"); Equal("Red", settings.LossTextColor, "default loss color"); Equal("Yellow", settings.DrawTextColor, "default draw color");
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

		private static void MatchIdentityUsesNewStatsHandleAfterMenu()
		{
			var snapshot = new GameHandleSnapshot(100, 200);
			var decision = MatchIdentityResolver.Evaluate(100, true, snapshot);
			Equal(MatchIdentityAction.StartNew, decision.Action, "new stats handle starts a new match");
			Equal<uint?>(200, decision.GameHandle, "new stats handle is selected");
		}

		private static void MatchIdentityPreservesMatchingHandleAfterMenu()
		{
			var decision = MatchIdentityResolver.Evaluate(100, true, new GameHandleSnapshot(100, 100));
			Equal(MatchIdentityAction.Preserve, decision.Action, "matching handles preserve state");
			Equal<uint?>(100, decision.GameHandle, "matching handle remains active");
		}

		private static void MatchIdentityWaitsForAmbiguousConflict()
		{
			var decision = MatchIdentityResolver.Evaluate(100, true, new GameHandleSnapshot(200, 300));
			Equal(MatchIdentityAction.Wait, decision.Action, "unrelated conflicting handles wait for stable data");
		}

		private static void MatchIdentityPreservesStoredHandleDuringActiveConflict()
		{
			var decision = MatchIdentityResolver.Evaluate(200, false, new GameHandleSnapshot(100, 200));
			Equal(MatchIdentityAction.Preserve, decision.Action, "active match keeps its known handle during a transient conflict");
			Equal<uint?>(200, decision.GameHandle, "known active handle is retained");
		}

		private static void MatchEndPolicyRequiresAConfirmedResult()
		{
			False(MatchEndPolicy.ShouldClearState(false, false), "missing game stats do not clear match state");
			False(MatchEndPolicy.ShouldClearState(true, false), "pending result does not clear match state");
			True(MatchEndPolicy.ShouldClearState(true, true), "confirmed result clears match state");
		}

		private static void CombatResultWinIsDetected()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false); tracker.RecordHeroDamage(2);
			Equal(CombatOutcome.Win, tracker.CompleteCombat(2, null, null, false, true), "opponent damage means a win");
		}

		private static void CombatResultLossIsDetected()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false); tracker.RecordHeroDamage(1);
			Equal(CombatOutcome.Loss, tracker.CompleteCombat(2, null, null, false, true), "local damage means a loss");
		}

		private static void CombatResultDrawIsDetected()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false);
			Equal(CombatOutcome.Draw, tracker.CompleteCombat(2, 40, 40, false, true), "completed combat without hero damage means a draw");
		}

		private static void ExactWinDamageIsCaptured()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 10, false); tracker.RecordHeroDamage(2, 15);
			var combat = tracker.CompleteCombatWithDamage(2, 40, 0, false, true);
			Equal(CombatOutcome.Win, combat.Outcome, "opponent durability loss means a win"); Equal<int?>(15, combat.ExactDamage, "live event preserves exact overkill damage");
		}

		private static void ExactLossDamageIsCaptured()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false); tracker.RecordHeroDamage(1, 10);
			var combat = tracker.CompleteCombatWithDamage(2, 30, 40, false, true);
			Equal(CombatOutcome.Loss, combat.Outcome, "local durability loss means a loss"); Equal<int?>(10, combat.ExactDamage, "exact loss damage");
		}

		private static void DrawDamageIsZero()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false);
			var combat = tracker.CompleteCombatWithDamage(2, 40, 40, false, true);
			Equal(CombatOutcome.Draw, combat.Outcome, "unchanged durability means a draw"); Equal<int?>(0, combat.ExactDamage, "draw damage is zero");
		}

		private static void RestoredLossRecoversDamageFromDurability()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false); tracker.RecordHeroDamage(1, 15); tracker.DiscardRecordedDamage();
			var combat = tracker.CompleteCombatWithDamage(2, 25, 40, false, false);
			Equal(CombatOutcome.Loss, combat.Outcome, "restored durability identifies the loss"); Equal<int?>(15, combat.ExactDamage, "loss damage is recovered from restored durability");
		}

		private static void RestoredWinRecoversDamageFromDurability()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false); tracker.RecordHeroDamage(2, 15); tracker.DiscardRecordedDamage();
			var combat = tracker.CompleteCombatWithDamage(2, 40, 25, false, false);
			Equal(CombatOutcome.Win, combat.Outcome, "restored durability identifies the win"); Equal<int?>(15, combat.ExactDamage, "win damage is recovered from restored durability");
		}

		private static void MissingDurabilityDoesNotInventDamage()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false);
			var combat = tracker.CompleteCombatWithDamage(2, null, null, true, false);
			Equal(CombatOutcome.Win, combat.Outcome, "win tag identifies the result"); Equal<int?>(null, combat.ExactDamage, "missing durability does not invent damage");
		}

		private static void InconsistentDamageEventUsesDurability()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false); tracker.RecordHeroDamage(2, 15);
			var combat = tracker.CompleteCombatWithDamage(2, 31, 40, false, true);
			Equal(CombatOutcome.Loss, combat.Outcome, "durability overrides the inconsistent event"); Equal<int?>(9, combat.ExactDamage, "damage is recovered from the consistent durability side");
		}

		private static void GhostExactDamageUsesTheDamageEvent()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 7, 40, -5, true); tracker.RecordHeroDamage(7, 12);
			var combat = tracker.CompleteCombatWithDamage(7, 40, -5, false, true);
			Equal(CombatOutcome.Win, combat.Outcome, "ghost win uses the damage event"); Equal<int?>(12, combat.ExactDamage, "exact ghost damage");
		}

		private static void UncertainCombatResultRemainsUnknown()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false);
			Equal(CombatOutcome.Unknown, tracker.CompleteCombat(2, null, null, false, false), "uncertain completion does not invent a draw");
		}

		private static void RestoredWinTagIsDetected()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false);
			Equal(CombatOutcome.Win, tracker.CompleteCombat(2, null, null, true, false), "last-combat win tag restores a missed win");
		}

		private static void DurabilityChangeOverridesAnIncorrectEvent()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, 40, 40, false); tracker.RecordHeroDamage(2);
			Equal(CombatOutcome.Loss, tracker.CompleteCombat(2, 31, 40, false, true), "durability loss identifies the damaged hero");
		}

		private static void GhostWinUsesTheDamageEvent()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 7, 40, -5, true); tracker.RecordHeroDamage(7);
			Equal(CombatOutcome.Win, tracker.CompleteCombat(7, 40, -5, false, true), "ghost win is detected without a durability change");
		}

		private static void CombatResultResetClearsActiveCombat()
		{
			var tracker = new CombatResultTracker(); tracker.StartCombat(1, 2, null, null, false); tracker.RecordHeroDamage(2); tracker.Reset();
			Equal(CombatOutcome.Unknown, tracker.CompleteCombat(2, null, null, false, true), "reset clears pending combat result");
		}

		private static void RestoredStateTransitionIsScopedToTheAffectedCombat()
		{
			True(CombatResultTracker.WasStateRestoredDuringCombat(false, true, 10, 20), "new restored state marks the interrupted combat");
			True(CombatResultTracker.WasStateRestoredDuringCombat(true, true, 10, 20), "changed client handle marks a later interrupted combat");
			False(CombatResultTracker.WasStateRestoredDuringCombat(true, true, 20, 20), "stable later combat can still be a draw");
		}

		private static void GameStartGenerationDetectsAnEarlyStateRestore()
		{
			True(CombatResultTracker.WasStateRestoredDuringCombat(false, false, 20, 20, 4, 5), "game start generation detects restoration before the delayed flag");
			False(CombatResultTracker.WasStateRestoredDuringCombat(false, false, 20, 20, 5, 5), "stable generation does not mark a normal combat");
		}

		private static void LateDamageIsAcceptedBeforeFinalization()
		{
			var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			var gate = new CombatCompletionGate();
			var tracker = new CombatResultTracker();
			tracker.StartCombat(1, 2, 40, 40, false);
			gate.Begin(false);
			False(gate.CanFinalize(now, true, true, true), "combat is not finalized immediately");
			tracker.RecordHeroDamage(2);
			True(gate.CanFinalize(now.AddMilliseconds(100), true, true, true), "late damage window eventually closes");
			Equal(CombatOutcome.Win, tracker.CompleteCombat(2, 40, 40, false, true), "late damage is used before finalization");
		}

		private static void InterruptedCombatWaitsForSupportedState()
		{
			var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			var gate = new CombatCompletionGate();
			gate.Begin(true);
			gate.Suspend();
			False(gate.CanFinalize(now, false, false, false), "unsupported state keeps the result pending");
			False(gate.CanFinalize(now.AddSeconds(10), true, true, true), "returning state starts a fresh stabilization window");
			True(gate.CanFinalize(now.AddSeconds(10).AddMilliseconds(100), true, true, true), "stable returned state can finalize");
		}

		private static void MenuInterruptionDoesNotEndTheCombat()
		{
			var gate = new CombatCompletionGate();
			gate.MarkInterrupted();
			False(gate.IsPending, "temporary menu state does not end an active combat");
			gate.Begin(false);
			True(gate.WasInterrupted, "interruption is retained until the real combat boundary");
		}

		private static void InterruptedCombatWaitsForCompletionState()
		{
			var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			var gate = new CombatCompletionGate();
			gate.Begin(true);
			False(gate.CanFinalize(now, true, false, true), "restored entities alone do not prove the combat ended");
			False(gate.CanFinalize(now.AddSeconds(5), true, true, true), "confirmed completion starts the result delay");
			True(gate.CanFinalize(now.AddSeconds(5).AddMilliseconds(100), true, true, true), "confirmed completion finalizes after the result delay");
		}

		private static void InterruptedLossUsesRestoredDurability()
		{
			var tracker = new CombatResultTracker();
			tracker.StartCombat(1, 2, 40, 40, false);
			Equal(CombatOutcome.Loss, tracker.CompleteCombat(2, 25, 40, false, false), "fifteen restored damage identifies a loss");
		}

		private static void RestoredDrawIgnoresReplayedDamage()
		{
			var tracker = new CombatResultTracker();
			tracker.StartCombat(1, 2, 25, 30, false);
			tracker.RecordHeroDamage(1, 15);
			tracker.DiscardRecordedDamage();
			var combat = tracker.CompleteCombatWithDamage(2, 25, 30, false, true);
			Equal(CombatOutcome.Draw, combat.Outcome, "replayed damage does not turn a restored draw into a loss"); Equal<int?>(0, combat.ExactDamage, "restored draw displays zero");
		}

		private static void MissingRestoredDataDoesNotBecomeADraw()
		{
			var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			var gate = new CombatCompletionGate();
			gate.Begin(true);
			False(gate.CanFinalize(now, true, true, false), "missing restored data starts a timeout");
			True(gate.CanFinalize(now.AddSeconds(3), true, true, false), "missing restored data eventually releases the pending combat");
		}

		private static void InterruptedStateDoesNotLeakIntoTheNextCombat()
		{
			var gate = new CombatCompletionGate();
			gate.MarkInterrupted();
			gate.Begin(false);
			gate.Reset();
			gate.Begin(false);
			False(gate.WasInterrupted, "next combat starts without the previous interruption");
		}

		private static void True(bool value, string name) { if(!value) throw new InvalidOperationException(name); }
		private static void False(bool value, string name) { if(value) throw new InvalidOperationException(name); }
		private static void Equal<T>(T expected, T actual, string name) { if(!Equals(expected, actual)) throw new InvalidOperationException(name + ": expected " + expected + ", actual " + actual); }
	}
}
