using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// One member of a reconcile group: its batch + the engine-computed match. The <see cref="MemberId"/>
/// is the grouped dimension (e.g. each partner of a merchant, or each merchant of a partner).
/// </summary>
public sealed record ReconciliationGroupMember(Guid MemberId, ReconciliationBatch Batch, ReconciliationMatch Match);

/// <summary>The per-member outcome of a grouped (partial) reconcile action.</summary>
public sealed record ReconciliationMemberResult(Guid MemberId, GroupReconcileOutcome Outcome);

/// <summary>
/// Grouped read model: each member's status plus the ready / exception / committed counts. A view over
/// the SAME source as the separate (single partner+period) status — never a different number.
/// </summary>
public sealed record GroupReconciliationView(
    IReadOnlyList<ReconciliationStatusReport> Members,
    int ReadyCount,
    int ExceptionCount,
    int CommittedCount);

/// <summary>
/// Grouped reconcile over the SAME single-member logic. PARTIAL by design: a group action commits the
/// ReadyToReconcile members and LEAVES Exceptions open (it never blocks the clean ones, and never
/// auto-overrides — an override is a deliberate, noted, per-member accountant action). Records/derives
/// state ONLY: posts no journal, moves no money.
/// </summary>
public static class GroupReconciliation
{
    /// <summary>
    /// Reconcile a group in one action. Clean members → Reconciled; Exceptions → left open
    /// (LeftAsException). Returns a per-member result. Idempotent per member (already-committed = no-op).
    /// </summary>
    public static IReadOnlyList<ReconciliationMemberResult> ReconcileGroup(
        IEnumerable<ReconciliationGroupMember> members,
        string reconciledBy,
        DateTime at)
    {
        Check.NotNull(members, nameof(members));

        var results = new List<ReconciliationMemberResult>();

        foreach (var member in members)
        {
            if (member.Match.ProposedState == ReconciliationProposedState.ReadyToReconcile)
            {
                member.Batch.Reconcile(member.Match, reconciledBy, at);
                results.Add(new ReconciliationMemberResult(member.MemberId, GroupReconcileOutcome.Reconciled));
            }
            else
            {
                // Leave the Exception open — committing it needs a deliberate override-with-note.
                results.Add(new ReconciliationMemberResult(member.MemberId, GroupReconcileOutcome.LeftAsException));
            }
        }

        return results;
    }

    /// <summary>The grouped status view with ready / exception / committed counts.</summary>
    public static GroupReconciliationView View(IEnumerable<ReconciliationGroupMember> members)
    {
        Check.NotNull(members, nameof(members));
        var list = members.ToList();

        var memberStatuses = list
            .Select(m => Reconciliation.Status(m.Match, m.Batch))
            .ToList();

        return new GroupReconciliationView(
            memberStatuses,
            memberStatuses.Count(s => s.ProposedState == ReconciliationProposedState.ReadyToReconcile),
            memberStatuses.Count(s => s.ProposedState == ReconciliationProposedState.Exception),
            memberStatuses.Count(s => s.IsReconciled));
    }
}
