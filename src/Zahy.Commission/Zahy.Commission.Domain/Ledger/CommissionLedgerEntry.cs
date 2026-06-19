using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Commission;

/// <summary>
/// Append-only commission ledger row. Monetary fields are immutable after insert.
/// Corrections arrive as reversal rows — accrual rows are never mutated in place.
/// </summary>
public class CommissionLedgerEntry : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid RuleId { get; private set; }

    public string SourceType { get; private set; } = string.Empty;

    public string SourceId { get; private set; } = string.Empty;

    public Guid? OrderRecordId { get; private set; }

    public decimal BasisAmount { get; private set; }

    public decimal ComputedCommission { get; private set; }

    public string Currency { get; private set; } = CommissionConsts.DefaultCurrency;

    public CommissionDirection Direction { get; private set; }

    public CommissionEntryKind EntryKind { get; private set; }

    public Guid? ReversesEntryId { get; private set; }

    public CommissionLedgerStatus Status { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public DateTime? PaidAt { get; private set; }

    protected CommissionLedgerEntry()
    {
    }

    public static CommissionLedgerEntry CreateAccrual(
        Guid id,
        Guid partnerId,
        Guid? tenantId,
        Guid ruleId,
        string sourceType,
        string sourceId,
        decimal basisAmount,
        decimal computedCommission,
        CommissionDirection direction,
        DateTime createdAt,
        Guid? orderRecordId = null,
        string currency = CommissionConsts.DefaultCurrency)
    {
        ValidateAccrualAmounts(basisAmount, computedCommission);
        ValidateSource(sourceType, sourceId);

        if (computedCommission < 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidLedgerEntry)
                .WithData("Field", nameof(computedCommission));
        }

        return new CommissionLedgerEntry
        {
            Id = id,
            PartnerId = partnerId,
            TenantId = tenantId,
            RuleId = ruleId,
            SourceType = sourceType.Trim(),
            SourceId = sourceId.Trim(),
            OrderRecordId = orderRecordId,
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Currency = NormalizeCurrency(currency),
            Direction = direction,
            EntryKind = CommissionEntryKind.Accrual,
            Status = CommissionLedgerStatus.Accrued,
            IdempotencyKey = CommissionLedgerIdempotency.BuildAccrualKey(sourceType, sourceId, ruleId),
            CreatedAt = createdAt
        };
    }

    public static CommissionLedgerEntry CreateReversal(
        Guid id,
        CommissionLedgerEntry original,
        DateTime createdAt)
    {
        Check.NotNull(original, nameof(original));

        if (original.EntryKind != CommissionEntryKind.Accrual)
        {
            throw new BusinessException(CommissionErrorCodes.ReversalNotAllowed);
        }

        return new CommissionLedgerEntry
        {
            Id = id,
            PartnerId = original.PartnerId,
            TenantId = original.TenantId,
            RuleId = original.RuleId,
            SourceType = original.SourceType,
            SourceId = original.SourceId,
            OrderRecordId = original.OrderRecordId,
            BasisAmount = original.BasisAmount,
            ComputedCommission = -original.ComputedCommission,
            Currency = original.Currency,
            Direction = original.Direction,
            EntryKind = CommissionEntryKind.Reversal,
            ReversesEntryId = original.Id,
            Status = CommissionLedgerStatus.Reversed,
            IdempotencyKey = CommissionLedgerIdempotency.BuildReversalKey(
                original.SourceType,
                original.SourceId,
                original.RuleId,
                original.Id),
            CreatedAt = createdAt
        };
    }

    public void Approve(DateTime approvedAt)
    {
        EnsureAccrual();
        if (Status != CommissionLedgerStatus.Accrued)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", CommissionLedgerStatus.Approved.ToString());
        }

        Status = CommissionLedgerStatus.Approved;
        ApprovedAt = approvedAt;
    }

    public void MarkPaid(DateTime paidAt)
    {
        EnsureAccrual();
        if (Status != CommissionLedgerStatus.Approved)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", CommissionLedgerStatus.Paid.ToString());
        }

        Status = CommissionLedgerStatus.Paid;
        PaidAt = paidAt;
    }

    private void EnsureAccrual()
    {
        if (EntryKind != CommissionEntryKind.Accrual)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidStatusTransition)
                .WithData("EntryKind", EntryKind.ToString());
        }
    }

    private static void ValidateAccrualAmounts(decimal basisAmount, decimal computedCommission)
    {
        if (basisAmount < 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisAmount);
        }

        if (computedCommission < 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidLedgerEntry)
                .WithData("Field", nameof(computedCommission));
        }
    }

    private static void ValidateSource(string sourceType, string sourceId)
    {
        Check.NotNullOrWhiteSpace(sourceType, nameof(sourceType));
        Check.NotNullOrWhiteSpace(sourceId, nameof(sourceId));

        if (sourceType.Length > CommissionConsts.MaxSourceTypeLength ||
            sourceId.Length > CommissionConsts.MaxSourceIdLength)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidLedgerEntry);
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        Check.NotNullOrWhiteSpace(currency, nameof(currency));
        return currency.Trim().ToUpperInvariant();
    }
}
