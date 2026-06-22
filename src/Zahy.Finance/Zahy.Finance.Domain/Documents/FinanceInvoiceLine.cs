using System;
using Volo.Abp;

namespace Zahy.Finance;

/// <summary>
/// A single line on a manual (ad-hoc) invoice. Owned by <see cref="FinanceDocument"/> — it has no
/// independent lifecycle and is loaded/saved with its parent document. VAT is split from the
/// VAT-INCLUSIVE line total using the SAME back-out convention as the rest of Finance/Settlement
/// (<see cref="FinanceVat"/>), so a manual invoice reconciles to the identical VAT model as a
/// ledger-sourced one. Plain (non-ABP) owned type so EF maps it as a child table without
/// ConfigureByConvention auditing columns.
/// </summary>
public class FinanceInvoiceLine
{
    /// <summary>1-based, contiguous within an invoice. Also part of the owned composite key.</summary>
    public int LineNo { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPriceInclusive { get; private set; }

    public decimal LineTotalInclusive { get; private set; }

    public decimal VatNet { get; private set; }

    public decimal VatAmount { get; private set; }

    /// <summary>Chart account the line posts to. Never null in storage — defaults to
    /// <see cref="FinanceConsts.DefaultManualLineAccountCode"/> (4200 Fee Revenue) when unspecified.</summary>
    public string AccountCode { get; private set; } = FinanceConsts.DefaultManualLineAccountCode;

    protected FinanceInvoiceLine()
    {
    }

    public static FinanceInvoiceLine Create(
        int lineNo,
        string description,
        decimal quantity,
        decimal unitPriceInclusive,
        decimal vatRate,
        string? accountCode = null)
    {
        if (lineNo < 1)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("LineNo", lineNo);
        }

        Check.NotNullOrWhiteSpace(description, nameof(description), FinanceConsts.MaxLineDescriptionLength);

        if (quantity <= 0m)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("LineNo", lineNo)
                .WithData("Quantity", quantity);
        }

        if (unitPriceInclusive <= 0m)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("LineNo", lineNo)
                .WithData("UnitPriceInclusive", unitPriceInclusive);
        }

        var lineTotalInclusive = FinanceMoney.RoundPosting(quantity * unitPriceInclusive);
        var vatNet = FinanceVat.NetOfInclusive(lineTotalInclusive, vatRate);
        var vatAmount = FinanceVat.VatOfInclusive(lineTotalInclusive, vatRate);

        var resolvedAccount = string.IsNullOrWhiteSpace(accountCode)
            ? FinanceConsts.DefaultManualLineAccountCode
            : accountCode.Trim();

        return new FinanceInvoiceLine
        {
            LineNo = lineNo,
            Description = description.Trim(),
            Quantity = quantity,
            UnitPriceInclusive = FinanceMoney.RoundPosting(unitPriceInclusive),
            LineTotalInclusive = lineTotalInclusive,
            VatNet = vatNet,
            VatAmount = vatAmount,
            AccountCode = resolvedAccount
        };
    }
}
