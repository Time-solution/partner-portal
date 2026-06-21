using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// The disbursable context for a partner+period, read from the reconcile layer (never recomputed):
/// what Zahy owes the partner (net 2100), the funds actually received, and whether the period is
/// reconciled. Build it from a <see cref="ReconciliationMatch"/> + <see cref="ReconciliationBatch"/>.
/// </summary>
public sealed record DisbursementContext(
    Money AmountOwedToPartner,
    Money FundsReceived,
    bool IsReconciled)
{
    public static DisbursementContext From(ReconciliationMatch match, ReconciliationBatch? batch)
    {
        Check.NotNull(match, nameof(match));
        return new DisbursementContext(
            match.AmountOwedToPartner,
            match.FundsReceived,
            batch?.IsReconciled ?? false);
    }

    /// <summary>The ceiling we may ever pay out: never more than is owed AND never more than is in.</summary>
    public decimal DisbursableCeiling =>
        SettlementMoney.Round(Math.Min(AmountOwedToPartner.Amount, FundsReceived.Amount));
}

/// <summary>Read model: the disburse status of one partner+period (mirrors the frontend shape).</summary>
public sealed record DisbursementStatusReport(
    Guid PartnerId,
    SettlementPeriod Period,
    Money AmountOwedToPartner,
    Money FundsReceived,
    Money DisbursedToDate,
    Money RemainingToDisburse,
    DisbursementPositionState State);

/// <summary>
/// Pure disburse coordinator over append-only <see cref="Disbursement"/> rows. Enforces the three
/// SYSTEM gates (reconciled / capped by funds received / not over payable) plus idempotent creation,
/// and derives the status read model. It records/derives intent ONLY — it neither releases the human
/// lock nor posts a journal; money never moves here.
/// </summary>
public static class Disbursements
{
    /// <summary>Cumulative net disbursed for a partner+period = normal rows − reversal rows.</summary>
    public static decimal NetDisbursed(Guid partnerId, SettlementPeriod period, IEnumerable<Disbursement> disbursements)
    {
        var scoped = disbursements
            .Where(d => d.PartnerId == partnerId && d.Period == period)
            .ToList();

        var paidOut = scoped.Where(d => !d.IsReversal).Sum(d => d.Amount);
        var reversed = scoped.Where(d => d.IsReversal).Sum(d => d.Amount);

        return SettlementMoney.Round(paidOut - reversed);
    }

    /// <summary>
    /// Enforces the three system gates for a NEW <paramref name="amount"/> on top of
    /// <paramref name="disbursedToDate"/>. Throws the matching <see cref="SettlementDisbursementErrorCodes"/>
    /// block. Money is never moved here.
    /// </summary>
    public static void EnsureCanDisburse(DisbursementContext ctx, decimal disbursedToDate, Money amount)
    {
        Check.NotNull(ctx, nameof(ctx));
        Check.NotNull(amount, nameof(amount));

        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.NonPositiveDisbursement)
                .WithData("Amount", amount.Amount);
        }

        // Gate 1 — must be reconciled.
        if (!ctx.IsReconciled)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedNotReconciled);
        }

        var prospective = SettlementMoney.Round(SettlementMoney.Round(disbursedToDate) + amount.Amount);

        // Gate 2 — never pay out more than is actually IN.
        if (prospective > SettlementMoney.Round(ctx.FundsReceived.Amount))
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedExceedsFundsReceived)
                .WithData("FundsReceived", ctx.FundsReceived.Amount)
                .WithData("DisbursedToDate", SettlementMoney.Round(disbursedToDate))
                .WithData("Attempted", amount.Amount);
        }

        // Gate 3 — never pay out more than the payable.
        if (prospective > SettlementMoney.Round(ctx.AmountOwedToPartner.Amount))
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.DisburseBlockedExceedsPayable)
                .WithData("AmountOwedToPartner", ctx.AmountOwedToPartner.Amount)
                .WithData("DisbursedToDate", SettlementMoney.Round(disbursedToDate))
                .WithData("Attempted", amount.Amount);
        }
    }

    /// <summary>
    /// Idempotent create: if <paramref name="idempotencyKey"/> already exists in <paramref name="existing"/>
    /// the SAME row is returned (double-submit safety) and the gates are NOT re-evaluated. Otherwise the
    /// three gates are enforced against the current net-disbursed total and a new LOCKED row is returned.
    /// </summary>
    public static Disbursement CreateOrGet(
        Guid id,
        Guid partnerId,
        SettlementPeriod period,
        Money amount,
        string idempotencyKey,
        DateTime date,
        DisbursementContext ctx,
        IEnumerable<Disbursement> existing)
    {
        var rows = existing?.ToList() ?? new List<Disbursement>();

        var key = (idempotencyKey ?? string.Empty).Trim();
        var dupe = rows.FirstOrDefault(d => string.Equals(d.IdempotencyKey, key, StringComparison.OrdinalIgnoreCase));
        if (dupe != null)
        {
            return dupe;
        }

        EnsureCanDisburse(ctx, NetDisbursed(partnerId, period, rows), amount);

        return Disbursement.CreateLocked(id, partnerId, period, amount, key, date);
    }

    /// <summary>Status read model: owed / received / disbursed-to-date / remaining / derived position.</summary>
    public static DisbursementStatusReport Status(
        Guid partnerId,
        SettlementPeriod period,
        DisbursementContext ctx,
        IEnumerable<Disbursement> disbursements)
    {
        Check.NotNull(ctx, nameof(ctx));

        var disbursed = NetDisbursed(partnerId, period, disbursements);
        var ceiling = ctx.DisbursableCeiling;
        var remaining = SettlementMoney.Round(Math.Max(0m, ceiling - disbursed));
        var currency = ctx.AmountOwedToPartner.Currency;

        return new DisbursementStatusReport(
            partnerId,
            period,
            ctx.AmountOwedToPartner,
            ctx.FundsReceived,
            Money.Of(disbursed, currency),
            Money.Of(remaining, currency),
            PositionOf(ctx, disbursed, remaining));
    }

    private static DisbursementPositionState PositionOf(DisbursementContext ctx, decimal disbursed, decimal remaining)
    {
        if (!ctx.IsReconciled)
        {
            return DisbursementPositionState.NotReconciled;
        }

        if (remaining <= 0m)
        {
            return DisbursementPositionState.FullyDisbursed;
        }

        return disbursed > 0m
            ? DisbursementPositionState.PartiallyDisbursed
            : DisbursementPositionState.ReadyToDisburse;
    }
}
