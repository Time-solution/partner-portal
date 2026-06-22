using System;
using Volo.Abp.Application.Dtos;

namespace Zahy.Settlement.BankRegistry;

/// <summary>A registered bank as shown to the accountant — account number is ALWAYS masked here.</summary>
public class BankAccountDto : EntityDto<Guid>
{
    /// <summary>Ledger sub-account code (110x) under the 1100 parent.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The roll-up parent this bank's sub-account belongs to (always 1100).</summary>
    public string ParentCode { get; set; } = BankLedgerCoding.ParentCode;

    public string Name { get; set; } = string.Empty;

    /// <summary>Masked account number (last 4 only) — the full value is never returned.</summary>
    public string MaskedAccountNumber { get; set; } = string.Empty;

    public string Currency { get; set; } = SettlementConsts.DefaultCurrency;

    public BankAccountStatus Status { get; set; }

    /// <summary>Optional future gateway-route key (SEAM ONLY — not wired to any live gateway).</summary>
    public string? GatewayMapping { get; set; }
}

public class CreateBankAccountInput
{
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = SettlementConsts.DefaultCurrency;
    public string? GatewayMapping { get; set; }
}

public class UpdateBankAccountInput
{
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = SettlementConsts.DefaultCurrency;
    public string? GatewayMapping { get; set; }
}
