using System.Collections.Generic;

namespace Zahy.Settlement;

/// <summary>
/// The settlement lifecycle is strictly linear: Collected → Allocated → Invoiced → Cleared →
/// Disbursed → Reconciled. Only forward, single-step transitions are legal; skips and back-moves
/// are rejected. Corrections happen via reversing journals (Phase 1), not by walking the state back.
/// </summary>
public static class SettlementStateMachine
{
    private static readonly IReadOnlyDictionary<SettlementCaseState, SettlementCaseState> Next =
        new Dictionary<SettlementCaseState, SettlementCaseState>
        {
            [SettlementCaseState.Collected] = SettlementCaseState.Allocated,
            [SettlementCaseState.Allocated] = SettlementCaseState.Invoiced,
            [SettlementCaseState.Invoiced] = SettlementCaseState.Cleared,
            [SettlementCaseState.Cleared] = SettlementCaseState.Disbursed,
            [SettlementCaseState.Disbursed] = SettlementCaseState.Reconciled
        };

    public static SettlementCaseState? NextOf(SettlementCaseState state) =>
        Next.TryGetValue(state, out var next) ? next : null;

    public static bool CanTransition(SettlementCaseState from, SettlementCaseState to) =>
        Next.TryGetValue(from, out var next) && next == to;

    public static bool IsTerminal(SettlementCaseState state) =>
        state == SettlementCaseState.Reconciled;
}
