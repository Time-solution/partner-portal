using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Zahy.Commission;

namespace Zahy.Settlement.Read;

/// <summary>Money contract for portal live-swap (mirrors domain three-part money).</summary>
public class MoneyDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = SettlementConsts.DefaultCurrency;
    public bool VatInclusive { get; set; }
}

public class SettlementJournalLineReadDto
{
    public SettlementAccountType Account { get; set; }
    public EntryDirection Direction { get; set; }
    public MoneyDto Amount { get; set; } = new();
}

/// <summary>Journal shape returned by read APIs — reconstructed from posted allocation snapshot.</summary>
public class SettlementJournalReadDto
{
    public string Currency { get; set; } = SettlementConsts.DefaultCurrency;
    public DateTime? PostedAt { get; set; }
    public string? Reference { get; set; }
    public Guid? ReversesJournalId { get; set; }
    public MoneyDto TotalDebits { get; set; } = new();
    public MoneyDto TotalCredits { get; set; } = new();
    public List<SettlementJournalLineReadDto> Lines { get; set; } = new();
}

public class SettlementCaseReadDto : EntityDto<Guid>
{
    public SettlementBook Book { get; set; }
    public Guid PartnerId { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public SettlementCaseState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ReversesSettlementCaseId { get; set; }
    public SettlementJournalReadDto? Journal { get; set; }
}

public class SettlementBillingChargeReadDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public Guid? TenantId { get; set; }
    public BillingChargeTarget ChargeTarget { get; set; }
    public BillingChargeKind Kind { get; set; }
    public MoneyDto Amount { get; set; } = new();
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? PeriodKey { get; set; }
    public string? Description { get; set; }
    public DateTime ChargedAt { get; set; }
    public List<Guid> LinkedSnapshotIds { get; set; } = new();
}

public class SettlementPartnerQuery
{
    public Guid? PartnerId { get; set; }
}
