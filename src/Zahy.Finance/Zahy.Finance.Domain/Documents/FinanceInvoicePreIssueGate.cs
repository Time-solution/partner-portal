using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Finance;

/// <summary>One gate finding: the architect-assigned error code plus a human-readable message.</summary>
public sealed record FinanceInvoiceViolation(string Code, string Message);

/// <summary>
/// P5 — everything the gate needs that the aggregate cannot know by itself, pre-fetched READ-ONLY by
/// the call sites (app service for manual drafts, generation service for ledger-derived births) and
/// handed in as plain data so the gate stays pure and deterministic.
/// </summary>
public sealed class FinanceInvoiceGateContext
{
    public DateTime NowUtc { get; init; }

    /// <summary>The configured VAT rate — the SAME single source (FinanceVatOptions) as everywhere else.</summary>
    public decimal VatRate { get; init; }

    /// <summary>Counterparty resolution (Partner/Merchant recipients only; External skips).</summary>
    public bool CounterpartyExists { get; init; } = true;

    public bool CounterpartyActive { get; init; } = true;

    /// <summary>True when the counterparty is a PARTNER whose participation is ReflectionOnly
    /// (derived from its catalog: ≥1 offering, all ReflectionOnly — no money relationship).</summary>
    public bool CounterpartyIsReflectionOnlyPartner { get; init; }

    /// <summary>True when ANOTHER document already carries this invoice reference.</summary>
    public bool DuplicateReferenceExists { get; init; }

    /// <summary>From <c>IFinancePeriodStatusProvider</c> — the null-object default answers open.</summary>
    public bool PeriodOpen { get; init; } = true;

    /// <summary>LedgerDerived only: the figure the generator read from the ledger. Null for Manual.</summary>
    public decimal? LedgerSourceFigure { get; init; }

    /// <summary>All lookups clean — for domain tests and paths that pre-verified externally.</summary>
    public static FinanceInvoiceGateContext AllClear(DateTime nowUtc, decimal vatRate) =>
        new() { NowUtc = nowUtc, VatRate = vatRate };
}

/// <summary>
/// P5 — the typed gate failure: carries the COMPLETE violation list (no fail-fast). The exception
/// code is the FIRST violation's code (checklist order); the full list rides in
/// <see cref="Violations"/> and in Data["ViolationCodes"].
/// </summary>
public class FinanceInvoiceGateException : BusinessException
{
    public IReadOnlyList<FinanceInvoiceViolation> Violations { get; }

    public FinanceInvoiceGateException(IReadOnlyList<FinanceInvoiceViolation> violations)
        : base(violations[0].Code, "Pre-invoice validation failed.")
    {
        Violations = violations;
        WithData("ViolationCodes", string.Join(",", violations.Select(v => v.Code)));
        WithData("ViolationCount", violations.Count);
    }
}

/// <summary>
/// P5 — THE pre-invoice validation gate (Q1 mechanical checklist, Q2(a) system gate only, Q3(a) hard
/// block with the FULL typed violation list, Q4 single ReflectionOnly check). ONE gate, TWO call
/// sites: inside <see cref="FinanceDocument.Issue"/> for manual drafts, and the LedgerDerived
/// generation path before the document is born Issued. PURE: invoice + context in, verdict out — no
/// side effects, no ledger writes (:087 is a read-only comparison of figures supplied by the caller).
/// Checklist partition: Manual-only → :081 lines/:080 totals/:083 VAT/:084 counterparty/:088
/// ReflectionOnly; both sources → :085 duplicate/:086 date; LedgerDerived-only → :087 ledger tie.
/// (:082 immutable-after-Issued stays a post-issue guard OUTSIDE this gate.)
/// </summary>
public static class FinanceInvoicePreIssueGate
{
    public static IReadOnlyList<FinanceInvoiceViolation> Validate(
        FinanceDocument document,
        FinanceInvoiceGateContext context)
    {
        var violations = new List<FinanceInvoiceViolation>();
        var isManual = document.Source == FinanceDocumentSource.Manual;

        if (isManual)
        {
            // [:081 absorbed] at least one line (the P4 Issue precondition, now aggregated).
            if (document.Lines.Count == 0)
            {
                violations.Add(new FinanceInvoiceViolation(
                    FinanceErrorCodes.ManualInvoiceInvalidLines,
                    "A manual invoice requires at least one line."));
            }

            // [:081 absorbed] non-positive qty/price — defense-in-depth; entry-time guards untouched.
            foreach (var line in document.Lines.Where(l => l.Quantity <= 0m || l.UnitPriceInclusive <= 0m))
            {
                violations.Add(new FinanceInvoiceViolation(
                    FinanceErrorCodes.ManualInvoiceInvalidLines,
                    $"Line {line.LineNo}: quantity and unit price must be greater than zero."));
            }

            // [:080 absorbed] header totals vs sum of lines — RECOMPUTED from qty × unit, never trusted.
            var recomputedSum = FinanceMoney.RoundPosting(
                document.Lines.Sum(l => FinanceMoney.RoundPosting(l.Quantity * l.UnitPriceInclusive)));
            if (document.Lines.Count > 0 && recomputedSum != document.PostingSum)
            {
                violations.Add(new FinanceInvoiceViolation(
                    FinanceErrorCodes.ManualInvoiceTotalsImbalanced,
                    $"Header total {document.PostingSum} does not equal the recomputed line sum {recomputedSum}."));
            }

            // [:083] per-line VAT split — recompute via the single shared VAT source and compare to stored.
            foreach (var line in document.Lines)
            {
                var expectedTotal = FinanceMoney.RoundPosting(line.Quantity * line.UnitPriceInclusive);
                var expectedNet = FinanceVat.NetOfInclusive(expectedTotal, context.VatRate);
                var expectedVat = FinanceVat.VatOfInclusive(expectedTotal, context.VatRate);
                if (line.LineTotalInclusive != expectedTotal || line.VatNet != expectedNet || line.VatAmount != expectedVat)
                {
                    violations.Add(new FinanceInvoiceViolation(
                        FinanceErrorCodes.InvoiceLineVatMismatch,
                        $"Line {line.LineNo}: stored VAT split does not match the shared-source recompute."));
                }
            }

            // [:084] counterparty missing or not Active (Partner/Merchant recipients; External skips).
            if (document.RecipientType is FinanceDocumentRecipientType.Partner or FinanceDocumentRecipientType.Merchant)
            {
                if (document.RecipientReference == null || !context.CounterpartyExists)
                {
                    violations.Add(new FinanceInvoiceViolation(
                        FinanceErrorCodes.InvoiceCounterpartyInvalid,
                        "The counterparty does not exist."));
                }
                else if (!context.CounterpartyActive)
                {
                    violations.Add(new FinanceInvoiceViolation(
                        FinanceErrorCodes.InvoiceCounterpartyInvalid,
                        "The counterparty is not Active."));
                }

                // [:088] ReflectionOnly partner — no money relationship; merchants skip.
                if (document.RecipientType == FinanceDocumentRecipientType.Partner &&
                    context.CounterpartyIsReflectionOnlyPartner)
                {
                    violations.Add(new FinanceInvoiceViolation(
                        FinanceErrorCodes.InvoiceCounterpartyReflectionOnly,
                        "The partner is ReflectionOnly — manual invoicing is blocked."));
                }
            }
        }

        // [:085] duplicate reference (both sources).
        if (context.DuplicateReferenceExists)
        {
            violations.Add(new FinanceInvoiceViolation(
                FinanceErrorCodes.InvoiceDuplicateReference,
                $"Another document already carries the reference '{document.InvoiceNumber}'."));
        }

        // [:086] date invalid — issue date in the future, or the accounting period is not open.
        if (document.GeneratedAt > context.NowUtc)
        {
            violations.Add(new FinanceInvoiceViolation(
                FinanceErrorCodes.InvoiceDateInvalid,
                "The issue date is in the future."));
        }
        if (!context.PeriodOpen)
        {
            violations.Add(new FinanceInvoiceViolation(
                FinanceErrorCodes.InvoiceDateInvalid,
                "The accounting period for the issue date is not open."));
        }

        // [:087] LedgerDerived only: header total must tie to the ledger-source figure (read-only).
        if (!isManual &&
            context.LedgerSourceFigure.HasValue &&
            document.PostingSum != FinanceMoney.RoundPosting(context.LedgerSourceFigure.Value))
        {
            violations.Add(new FinanceInvoiceViolation(
                FinanceErrorCodes.InvoiceLedgerFigureMismatch,
                $"Header total {document.PostingSum} does not tie to the ledger figure {context.LedgerSourceFigure.Value}."));
        }

        return violations;
    }

    /// <summary>Hard block: evaluates ALL checks and throws ONE exception with the complete list.</summary>
    public static void EnsureValid(FinanceDocument document, FinanceInvoiceGateContext context)
    {
        var violations = Validate(document, context);
        if (violations.Count > 0)
        {
            throw new FinanceInvoiceGateException(violations);
        }
    }
}
