using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Commission;

public interface ICommissionLedgerService
{
    Task<CommissionLedgerAccrualResult> AccrueAsync(
        CommissionAccrualRequest request,
        CancellationToken cancellationToken = default);

    Task<List<CommissionLedgerEntryDto>> GetListAsync(
        CommissionLedgerListInput input,
        CancellationToken cancellationToken = default);

    Task<CommissionLedgerEntryDto> ApproveAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    Task<CommissionLedgerEntryDto> MarkPaidAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);

    Task<CommissionLedgerReversalResult> ReverseAsync(
        Guid originalEntryId,
        ReverseCommissionLedgerInput input,
        CancellationToken cancellationToken = default);
}

public sealed class CommissionLedgerListInput
{
    public CommissionLedgerStatus? Status { get; init; }

    public Guid? PartnerId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class ReverseCommissionLedgerInput
{
    public string Reason { get; init; } = string.Empty;

    public string? IdempotencyKey { get; init; }
}

public sealed class CommissionAccrualRequest
{
    public Guid PartnerId { get; init; }

    public Guid? TenantId { get; init; }

    public Guid RuleId { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public Guid? OrderRecordId { get; init; }

    public decimal BasisAmount { get; init; }

    public decimal ComputedCommission { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public CommissionDirection Direction { get; init; }
}

public sealed class CommissionLedgerAccrualResult
{
    public Guid EntryId { get; init; }

    public bool IsNew { get; init; }

    public CommissionDirection Direction { get; init; }

    public decimal BasisAmount { get; init; }

    public decimal ComputedCommission { get; init; }

    public CommissionLedgerStatus Status { get; init; }
}

public sealed class CommissionLedgerReversalResult
{
    public Guid ReversalEntryId { get; init; }

    public Guid OriginalEntryId { get; init; }

    public bool IsNew { get; init; }

    public decimal OriginalComputedCommission { get; init; }

    public decimal ReversalComputedCommission { get; init; }
}

public sealed class CommissionLedgerEntryDto
{
    public Guid Id { get; init; }

    public Guid PartnerId { get; init; }

    public Guid? TenantId { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public CommissionDirection Direction { get; init; }

    public decimal BasisAmount { get; init; }

    public decimal ComputedCommission { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public CommissionLedgerStatus Status { get; init; }

    public CommissionEntryKind EntryKind { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ApprovedAt { get; init; }

    public Guid? ApprovedByUserId { get; init; }

    public DateTime? PaidAt { get; init; }

    public Guid? ReversesEntryId { get; init; }
}
