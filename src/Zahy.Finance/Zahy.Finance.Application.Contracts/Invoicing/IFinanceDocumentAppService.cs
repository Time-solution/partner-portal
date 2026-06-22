using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.Finance;

/// <summary>
/// Ad-hoc (manual) invoice authoring — ADDITIVE to the ledger-sourced invoice pipeline. Lets an
/// accountant / platform admin key in a one-off invoice with custom recipient, line items, and notes,
/// independent of any order / settlement case / billing period.
/// </summary>
public interface IFinanceDocumentAppService : IApplicationService
{
    Task<FinanceDocumentDto> CreateManualInvoiceAsync(
        CreateManualInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceDocumentDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<FinanceDocumentBytesResult> GetPdfAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public sealed class CreateManualInvoiceRequest
{
    public FinanceDocumentRecipientType RecipientType { get; set; }

    /// <summary>Partner or Merchant id (required for Partner/Merchant; ignored for External).</summary>
    public Guid? RecipientReference { get; set; }

    /// <summary>Free-text recipient name. Required for External; overrides the looked-up name otherwise.</summary>
    [MaxLength(FinanceConsts.MaxRecipientLength)]
    public string? RecipientNameOverride { get; set; }

    // NOTE: the >= 1 line rule is enforced by the domain guard (throws BusinessException
    // ManualInvoiceInvalidLines) so the failure carries the Finance error code rather than a generic
    // validation error. Deliberately no [MinLength] here, which would preempt it with AbpValidationException.
    [Required]
    public List<ManualInvoiceLineDto> Lines { get; set; } = new();

    [MaxLength(FinanceConsts.MaxNotesLength)]
    public string? Notes { get; set; }

    public DateTime? IssueDate { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = FinanceConsts.DefaultCurrency;

    [Required]
    [MaxLength(FinanceConsts.MaxIdempotencyKeyLength)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class ManualInvoiceLineDto
{
    [Required]
    [MaxLength(FinanceConsts.MaxLineDescriptionLength)]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPriceInclusive { get; set; }

    [MaxLength(FinanceConsts.MaxAccountCodeLength)]
    public string? AccountCode { get; set; }
}

public sealed class FinanceDocumentDto
{
    public Guid Id { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public int FiscalYear { get; set; }

    public int SequenceNumber { get; set; }

    public FinanceDocumentSource Source { get; set; }

    public string? Recipient { get; set; }

    public FinanceDocumentRecipientType? RecipientType { get; set; }

    public Guid? RecipientReference { get; set; }

    public string? Notes { get; set; }

    public string Currency { get; set; } = FinanceConsts.DefaultCurrency;

    public DateTime IssueDate { get; set; }

    /// <summary>Sum of line totals (VAT-inclusive). Equals <see cref="GrandTotalInclusive"/> for manual invoices.</summary>
    public decimal PostingSum { get; set; }

    public decimal SubtotalNet { get; set; }

    public decimal VatTotal { get; set; }

    public decimal GrandTotalInclusive { get; set; }

    public List<FinanceInvoiceLineDto> Lines { get; set; } = new();

    /// <summary>Base64-encoded PDF rendered from the keyed input (manual) or ledger snapshot (derived).</summary>
    public string? PdfBase64 { get; set; }

    /// <summary>Token the client passes to GET /api/finance/invoices/{token}/pdf — the document id.</summary>
    public Guid PdfDownloadToken { get; set; }
}

public sealed class FinanceInvoiceLineDto
{
    public int LineNo { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPriceInclusive { get; set; }

    public decimal LineTotalInclusive { get; set; }

    public decimal VatNet { get; set; }

    public decimal VatAmount { get; set; }

    public string AccountCode { get; set; } = string.Empty;
}
