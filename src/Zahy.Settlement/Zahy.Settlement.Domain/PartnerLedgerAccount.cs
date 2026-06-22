using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Settlement;

/// <summary>
/// A partner's pair of ledger sub-accounts in the per-partner registry (the partner analogue of the
/// Track B <see cref="BankAccount"/>). When a partner becomes active in settlement it is bound to a
/// payable sub-account <see cref="PayableCode"/> (2101+ under 2100) and a receivable sub-account
/// <see cref="ReceivableCode"/> (1251+ under 1250), labelled with the partner name — so each 3PL/partner
/// has its OWN payable/receivable ledger line, not just a balance derived by filtering by PartnerId.
///
/// COMPUTE/REGISTRY ONLY — assigning these sub-codes does NOT mutate the production chart of accounts;
/// the live chart change stays gated (<c>SettlementEngineOptions.PartnerLedgerLiveChartEnabled</c>,
/// default OFF), exactly like the bank sub-accounts.
/// </summary>
public class PartnerLedgerAccount : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public string PartnerName { get; private set; } = string.Empty;

    /// <summary>Payable sub-account code (2101–2149) under the 2100 AP-Partner parent.</summary>
    public string PayableCode { get; private set; } = string.Empty;

    /// <summary>Receivable sub-account code (1251–1299) under the 1250 AR-Partner parent.</summary>
    public string ReceivableCode { get; private set; } = string.Empty;

    public PartnerLedgerAccountStatus Status { get; private set; } = PartnerLedgerAccountStatus.Active;

    protected PartnerLedgerAccount()
    {
    }

    public PartnerLedgerAccount(
        Guid id,
        Guid partnerId,
        string partnerName,
        string payableCode,
        string receivableCode)
        : base(id)
    {
        PartnerId = partnerId;
        PartnerName = NormalizeName(partnerName);
        PayableCode = NormalizePayable(payableCode);
        ReceivableCode = NormalizeReceivable(receivableCode);
        Status = PartnerLedgerAccountStatus.Active;
    }

    public PartnerLedgerAccount Rename(string partnerName)
    {
        PartnerName = NormalizeName(partnerName);
        return this;
    }

    public PartnerLedgerAccount Deactivate()
    {
        Status = PartnerLedgerAccountStatus.Inactive;
        return this;
    }

    public PartnerLedgerAccount Activate()
    {
        Status = PartnerLedgerAccountStatus.Active;
        return this;
    }

    private static string NormalizeName(string partnerName)
    {
        if (string.IsNullOrWhiteSpace(partnerName))
        {
            throw new BusinessException(SettlementPartnerLedgerErrorCodes.EmptyPartnerName);
        }

        return Check.NotNullOrWhiteSpace(
            partnerName, nameof(partnerName), SettlementPartnerLedgerConsts.MaxPartnerNameLength).Trim();
    }

    private static string NormalizePayable(string code)
    {
        var value = (code ?? string.Empty).Trim();
        if (!PartnerLedgerCoding.IsPayableSubAccount(value))
        {
            throw new BusinessException(SettlementPartnerLedgerErrorCodes.SubAccountRangeExhausted)
                .WithData("Code", value)
                .WithData("Expected", $"{PartnerLedgerCoding.FirstPayableSubCode}-{PartnerLedgerCoding.LastPayableSubCode}");
        }

        return value;
    }

    private static string NormalizeReceivable(string code)
    {
        var value = (code ?? string.Empty).Trim();
        if (!PartnerLedgerCoding.IsReceivableSubAccount(value))
        {
            throw new BusinessException(SettlementPartnerLedgerErrorCodes.SubAccountRangeExhausted)
                .WithData("Code", value)
                .WithData("Expected", $"{PartnerLedgerCoding.FirstReceivableSubCode}-{PartnerLedgerCoding.LastReceivableSubCode}");
        }

        return value;
    }
}
