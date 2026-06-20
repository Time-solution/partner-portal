using System;
using System.Collections.Generic;
using Volo.Abp;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Balanced journal for Zahy's own recurring channel fee — output VAT on the fee only.
/// No input VAT reclaim; no refund path on monthly fee (accountant-approved 2c).
/// </summary>
public static class SubscriptionFeeJournalBuilder
{
    public static Journal Build(
        Money feeInclusive,
        decimal vatRate,
        DateTime postedAt,
        Guid journalId,
        string? reference = null)
    {
        Check.NotNull(feeInclusive, nameof(feeInclusive));
        VatMath.EnsureValidRate(vatRate);

        if (!feeInclusive.VatInclusive)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidSubscriptionFee)
                .WithData("Reason", "FeeMustBeVatInclusive");
        }

        var currency = feeInclusive.Currency;
        var netFee = VatMath.NetOfInclusive(feeInclusive.Amount, vatRate);
        var outputVat = SettlementMoney.Round(feeInclusive.Amount - netFee);

        var lines = new List<JournalLine>
        {
            JournalLine.Debit(SettlementAccountType.PartnerPayable, feeInclusive),
            JournalLine.Credit(SettlementAccountType.PlatformCommissionRevenue, Money.Of(netFee, currency)),
            JournalLine.Credit(SettlementAccountType.VatOutput, Money.Of(outputVat, currency))
        };

        return Journal.Create(journalId, postedAt, lines, reference);
    }
}
