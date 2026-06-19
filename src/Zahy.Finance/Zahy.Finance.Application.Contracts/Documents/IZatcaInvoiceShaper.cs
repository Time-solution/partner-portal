namespace Zahy.Finance;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Local mirror of ZATCA Fatoora invoice DTO — no external package wiring in Step 4.</summary>
public interface IZatcaInvoiceShaper
{
    ZatcaFatooraInvoiceDto Shape(FinanceInvoiceDraft draft);
}

public interface IZatcaSubmitter
{
    Task<ZatcaSubmissionResult> SubmitAsync(
        ZatcaFatooraInvoiceDto invoice,
        CancellationToken cancellationToken = default);
}

public sealed class FinanceInvoiceDraft
{
    public string InvoiceNumber { get; init; } = string.Empty;

    public DateTime IssueDate { get; init; }

    public decimal TaxExclusiveAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TaxInclusiveAmount { get; init; }

    public string Currency { get; init; } = FinanceConsts.DefaultCurrency;

    public FinanceDocumentKycBlockDto Seller { get; init; } = new();

    public FinanceDocumentKycBlockDto Buyer { get; init; } = new();
}

public sealed class ZatcaFatooraInvoiceDto
{
    public string Uuid { get; init; } = string.Empty;

    public string InvoiceNumber { get; init; } = string.Empty;

    public DateTime IssueDate { get; init; }

    public string Currency { get; init; } = FinanceConsts.DefaultCurrency;

    public decimal TaxExclusiveAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TaxInclusiveAmount { get; init; }

    public string SellerLegalNameEn { get; init; } = string.Empty;

    public string SellerVatNumber { get; init; } = string.Empty;

    public string BuyerLegalNameEn { get; init; } = string.Empty;

    public string BuyerVatNumber { get; init; } = string.Empty;

    public string QrCodePlaceholder { get; init; } = string.Empty;
}

public sealed class ZatcaSubmissionResult
{
    public bool Submitted { get; init; }

    public string Message { get; init; } = string.Empty;
}
