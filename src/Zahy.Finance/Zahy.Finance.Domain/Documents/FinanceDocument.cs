using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class FinanceDocument : AggregateRoot<Guid>
{
    private readonly List<FinanceInvoiceLine> _lines = new();

    public FinanceAccountKind AccountKind { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid EntityId { get; private set; }

    public FinanceDocumentKind DocumentKind { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string InvoiceNumber { get; private set; } = string.Empty;

    public int FiscalYear { get; private set; }

    public int SequenceNumber { get; private set; }

    public decimal PostingSum { get; private set; }

    public string Currency { get; private set; } = FinanceConsts.DefaultCurrency;

    public DateTime GeneratedAt { get; private set; }

    public Guid? SourceRowId { get; private set; }

    // ----- Manual (ad-hoc) invoice extensions. Empty / null for LedgerDerived documents. -----

    /// <summary>How this document was produced. LedgerDerived (default) keeps every pre-existing
    /// invoice's behaviour byte-identical; Manual carries line items + a recipient.</summary>
    public FinanceDocumentSource Source { get; private set; } = FinanceDocumentSource.LedgerDerived;

    /// <summary>P4 lifecycle — LedgerDerived documents are born Issued (generated final); manual
    /// invoices start Draft and take exactly one <see cref="Issue"/> transition. Default matches the
    /// migration backfill (Issued) so every pre-existing row keeps its final semantics.</summary>
    public FinanceDocumentStatus Status { get; private set; } = FinanceDocumentStatus.Issued;

    /// <summary>Human-readable recipient name (required for Manual). Null for LedgerDerived.</summary>
    public string? Recipient { get; private set; }

    public FinanceDocumentRecipientType? RecipientType { get; private set; }

    /// <summary>Partner or Merchant id the manual invoice is addressed to (null for External).</summary>
    public Guid? RecipientReference { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Line items. Owned by the document; only populated for Manual invoices.</summary>
    public IReadOnlyCollection<FinanceInvoiceLine> Lines => new ReadOnlyCollection<FinanceInvoiceLine>(_lines);

    protected FinanceDocument()
    {
    }

    public static FinanceDocument CreateInvoice(
        Guid id,
        FinanceAccountKind accountKind,
        Guid accountId,
        Guid entityId,
        string idempotencyKey,
        string invoiceNumber,
        int fiscalYear,
        int sequenceNumber,
        decimal postingSum,
        DateTime generatedAt,
        Guid? sourceRowId = null,
        string currency = FinanceConsts.DefaultCurrency)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));

        return new FinanceDocument
        {
            Id = id,
            AccountKind = accountKind,
            AccountId = accountId,
            EntityId = entityId,
            DocumentKind = FinanceDocumentKind.Invoice,
            IdempotencyKey = idempotencyKey.Trim(),
            InvoiceNumber = invoiceNumber.Trim(),
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            PostingSum = FinanceMoney.RoundPosting(postingSum),
            Currency = currency,
            GeneratedAt = generatedAt,
            SourceRowId = sourceRowId,
            Source = FinanceDocumentSource.LedgerDerived,
            Status = FinanceDocumentStatus.Issued
        };
    }

    /// <summary>
    /// Creates an ad-hoc (manual) invoice from keyed-in line items — ADDITIVE to, and independent of,
    /// the ledger-sourced generator. <paramref name="declaredPostingSum"/> is the caller's computed
    /// total; <see cref="EnsureManualInvoiceTotalsBalance"/> verifies it equals the sum of line totals
    /// (inclusive) and throws <see cref="FinanceErrorCodes.ManualInvoiceTotalsImbalanced"/> (080) otherwise.
    /// </summary>
    public static FinanceDocument CreateManualInvoice(
        Guid id,
        FinanceDocumentRecipientType recipientType,
        Guid? recipientReference,
        string recipient,
        string idempotencyKey,
        string invoiceNumber,
        int fiscalYear,
        int sequenceNumber,
        IReadOnlyList<FinanceInvoiceLine> lines,
        decimal declaredPostingSum,
        DateTime generatedAt,
        string? notes = null,
        string currency = FinanceConsts.DefaultCurrency)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));
        Check.NotNullOrWhiteSpace(recipient, nameof(recipient), FinanceConsts.MaxRecipientLength);

        if (lines == null || lines.Count == 0)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "A manual invoice requires at least one line.");
        }

        // External recipients have no finance account / entity to scope to; Partner/Merchant recipients
        // are scoped to their reference so per-entity query filters behave (and partners never see another
        // entity's manual invoice).
        var accountKind = recipientType == FinanceDocumentRecipientType.Merchant
            ? FinanceAccountKind.Merchant
            : FinanceAccountKind.Partner;
        var entityId = recipientType == FinanceDocumentRecipientType.External
            ? Guid.Empty
            : recipientReference ?? Guid.Empty;

        var document = new FinanceDocument
        {
            Id = id,
            AccountKind = accountKind,
            AccountId = Guid.Empty,
            EntityId = entityId,
            DocumentKind = FinanceDocumentKind.Invoice,
            IdempotencyKey = idempotencyKey.Trim(),
            InvoiceNumber = invoiceNumber.Trim(),
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            PostingSum = FinanceMoney.RoundPosting(declaredPostingSum),
            Currency = currency,
            GeneratedAt = generatedAt,
            SourceRowId = null,
            Source = FinanceDocumentSource.Manual,
            Status = FinanceDocumentStatus.Draft,
            Recipient = recipient.Trim(),
            RecipientType = recipientType,
            RecipientReference = recipientReference,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        document._lines.AddRange(lines);

        document.EnsureManualInvoiceTotalsBalance();
        return document;
    }

    /// <summary>
    /// P4 — the single Draft → Issued transition. Requires at least one line (a manual draft cannot be
    /// issued empty) and throws <see cref="FinanceErrorCodes.IllegalStatusTransition"/> (009) from any
    /// state other than Draft. After Issue the document is immutable (see <see cref="ReplaceLines"/>).
    /// </summary>
    public void Issue()
    {
        if (Status != FinanceDocumentStatus.Draft)
        {
            throw new BusinessException(FinanceErrorCodes.IllegalStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", FinanceDocumentStatus.Issued.ToString());
        }

        if (Source == FinanceDocumentSource.Manual && _lines.Count == 0)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "A manual invoice requires at least one line to be issued.");
        }

        EnsureManualInvoiceTotalsBalance();
        RunPreIssueValidationGate();

        Status = FinanceDocumentStatus.Issued;
    }

    /// <summary>
    /// P5 SEAM — the pre-invoice validation gate will slot in HERE (single entry point, called by
    /// <see cref="Issue"/> just before the transition). Intentionally empty: P5 is a separate design
    /// phase and no validation logic may be built yet.
    /// </summary>
    private void RunPreIssueValidationGate()
    {
    }

    /// <summary>
    /// P4 — replace a DRAFT manual invoice's lines (whole-document replace; EF reconciles the owned
    /// collection as DELETE+INSERT). LINE-NUMBER RULE: RENUMBER-ON-REMOVE — callers pass the surviving
    /// lines re-built contiguously 1..N. Justification: a Draft is a pre-audit workspace and the Issued
    /// snapshot is the immutable audit record, so contiguous renumbering before issue breaks no history.
    /// Validate-then-mutate: any invalid input throws BEFORE the collection changes, so a failed add
    /// leaves the existing 1..N intact (no gaps). Throws <see cref="FinanceErrorCodes.ManualInvoiceImmutable"/>
    /// (082) once Issued.
    /// </summary>
    public void ReplaceLines(IReadOnlyList<FinanceInvoiceLine> lines)
    {
        if (Source != FinanceDocumentSource.Manual)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "Only manual invoices carry editable lines.");
        }

        if (Status != FinanceDocumentStatus.Draft)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceImmutable)
                .WithData("Status", Status.ToString());
        }

        if (lines == null || lines.Count == 0)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "A manual invoice requires at least one line.");
        }

        // Contiguity 1..N asserted BEFORE any mutation (validate-then-mutate).
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].LineNo != i + 1)
            {
                throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                    .WithData("Reason", "Line numbers must be contiguous 1..N.")
                    .WithData("Position", i + 1)
                    .WithData("LineNo", lines[i].LineNo);
            }
        }

        _lines.Clear();
        _lines.AddRange(lines);
        PostingSum = FinanceMoney.RoundPosting(_lines.Sum(x => x.LineTotalInclusive));

        EnsureManualInvoiceTotalsBalance();
    }

    /// <summary>
    /// Money invariant for manual invoices: the persisted PostingSum MUST equal the sum of line totals
    /// (VAT-inclusive). No-op for LedgerDerived documents (their sum comes from the ledger snapshot).
    /// </summary>
    public void EnsureManualInvoiceTotalsBalance()
    {
        if (Source != FinanceDocumentSource.Manual)
        {
            return;
        }

        var lineSum = FinanceMoney.RoundPosting(_lines.Sum(x => x.LineTotalInclusive));
        if (lineSum != PostingSum)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceTotalsImbalanced)
                .WithData("PostingSum", PostingSum)
                .WithData("LineSum", lineSum);
        }
    }
}
