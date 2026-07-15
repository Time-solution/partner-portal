namespace Zahy.Finance;

public static class FinanceConsts
{
    public const string DefaultCurrency = "SAR";

    public static readonly Guid PlatformSettingsId = new("11111111-1111-1111-1111-111111111001");

    /// <summary>Fixed partner id for Development seed data (partner@zahy.dev).</summary>
    public static readonly Guid DevPartnerId = new("22222222-2222-2222-2222-222222222001");

    public const int MaxIdempotencyKeyLength = 512;
    public const int MaxDescriptionLength = 512;
    public const int MaxSourceTypeLength = 64;
    public const int MaxSourceIdLength = 256;

    // ----- Manual (ad-hoc) invoice line items -----
    public const int MaxRecipientLength = 200;
    public const int MaxRecipientReferenceLength = 64;
    public const int MaxNotesLength = 2000;
    public const int MaxLineDescriptionLength = 500;
    public const int MaxAccountCodeLength = 32;

    /// <summary>Default chart account for manual-invoice lines when none is supplied (4200 — Fee Revenue).</summary>
    public const string DefaultManualLineAccountCode = "4200";
}

public static class FinanceMoney
{
    public const int PostingScale = 2;

    public static decimal RoundPosting(decimal value) =>
        Math.Round(value, PostingScale, MidpointRounding.AwayFromZero);
}

/// <summary>
/// VAT-inclusive back-out arithmetic for Zahy.Finance documents. This is the SAME convention used by the
/// Settlement engine (Zahy.Settlement.VatMath / the frontend splitInclusiveVat): money amounts crossing
/// the module boundary (billing charges, commission accruals) are VAT-INCLUSIVE, so a Finance invoice and
/// a Settlement journal split the SAME inclusive amount into the SAME net + VAT.
///
/// net = round(inclusive / (1 + rate), 2) and vat = round(inclusive − net, 2). The back-out (inclusive − net)
/// — rather than net × rate — keeps inclusive == net + vat exactly at 2dp. Rounding is 2dp AwayFromZero
/// (FinanceMoney.RoundPosting), byte-identical to SettlementMoney.Round.
/// </summary>
public static class FinanceVat
{
    public static void EnsureValidRate(decimal rate)
    {
        if (rate < 0m || rate >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "VAT rate must be in [0, 1).");
        }
    }

    /// <summary>Net (VAT-exclusive) amount of a VAT-inclusive price, rounded per line.</summary>
    public static decimal NetOfInclusive(decimal inclusiveAmount, decimal rate)
    {
        EnsureValidRate(rate);
        return FinanceMoney.RoundPosting(inclusiveAmount / (1m + rate));
    }

    /// <summary>VAT portion of a VAT-inclusive price (back-out): inclusive − net.</summary>
    public static decimal VatOfInclusive(decimal inclusiveAmount, decimal rate) =>
        FinanceMoney.RoundPosting(inclusiveAmount - NetOfInclusive(inclusiveAmount, rate));
}

public static class FinancePostingIdempotency
{
    public static string BuildCommissionKey(Guid ledgerEntryId) =>
        $"commission:accrual:{ledgerEntryId:N}";

    public static string BuildBillingKey(Guid billingChargeId) =>
        $"billing:charge:{billingChargeId:N}";
}

public static class FinanceErrorCodes
{
    public const string Namespace = "Zahy.Finance";

    public const string KycNotVerified = Namespace + ":001";
    public const string AccountNotFound = Namespace + ":002";
    public const string AccountNotActive = Namespace + ":003";
    public const string InvalidPosting = Namespace + ":004";
    public const string AccessDenied = Namespace + ":005";
    public const string IllegalKycTransition = Namespace + ":006";
    public const string KycVerificationNotFound = Namespace + ":007";
    public const string KycSubmissionNotFound = Namespace + ":008";
    public const string IllegalStatusTransition = Namespace + ":009";
    public const string InvoiceGenerationFailed = Namespace + ":010";

    /// <summary>A manual invoice's declared PostingSum does not equal the sum of its line totals (inclusive).</summary>
    public const string ManualInvoiceTotalsImbalanced = Namespace + ":080";

    /// <summary>A manual invoice was submitted with no line items, or a line failed validation.</summary>
    public const string ManualInvoiceInvalidLines = Namespace + ":081";

    /// <summary>P4 — an Issued manual invoice is immutable: its lines can never be replaced.</summary>
    public const string ManualInvoiceImmutable = Namespace + ":082";

    // ---- P5 pre-invoice validation gate (architect-assigned map — do not renumber) ----

    /// <summary>P5 — a line's stored VAT split does not match the recompute via the single VAT source.</summary>
    public const string InvoiceLineVatMismatch = Namespace + ":083";

    /// <summary>P5 — counterparty missing or not Active.</summary>
    public const string InvoiceCounterpartyInvalid = Namespace + ":084";

    /// <summary>P5 — another document already carries this invoice reference.</summary>
    public const string InvoiceDuplicateReference = Namespace + ":085";

    /// <summary>P5 — issue date invalid (in the future, or the accounting period is not open).</summary>
    public const string InvoiceDateInvalid = Namespace + ":086";

    /// <summary>P5 — LedgerDerived only: header total does not tie to the ledger-source figure.</summary>
    public const string InvoiceLedgerFigureMismatch = Namespace + ":087";

    /// <summary>P5 — counterparty partner is ReflectionOnly (no money relationship): manual invoicing blocked.</summary>
    public const string InvoiceCounterpartyReflectionOnly = Namespace + ":088";
}

public static class FinanceDocumentIdempotency
{
    public static string BuildBillingInvoiceKey(Guid billingChargeId) =>
        $"invoice:billing:{billingChargeId:N}";
}

public static class FinanceInvoiceNumberFormat
{
    public const string InvoicePrefix = "ZAHY-INV";

    /// <summary>P4 — internal manual-invoice reference prefix. Deliberately distinct from the fiscal
    /// pipeline: NOT a ZATCA &lt;cbc:ID&gt;, no ICV semantics (that pipeline is Main-side and gated).</summary>
    public const string ManualPrefix = "MAN";

    public static string Format(int fiscalYear, int sequenceNumber) =>
        $"{InvoicePrefix}-{fiscalYear}-{sequenceNumber:D6}";

    /// <summary>MAN-yyyy-#### — clearly non-fiscal, BETA-labeled at render time.</summary>
    public static string FormatManual(int fiscalYear, int sequenceNumber) =>
        $"{ManualPrefix}-{fiscalYear}-{sequenceNumber:D4}";
}

public sealed class FinanceInvoiceNumberAllocation
{
    public int FiscalYear { get; init; }

    public int SequenceNumber { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;
}

public static class FinanceKycConsts
{
    public const int MaxLegalNameLength = 256;
    public const int MaxCrNumberLength = 32;
    public const int MaxVatNumberLength = 32;
    public const int MaxIbanLength = 34;
    public const int MaxAddressLength = 512;
    public const int MaxReviewNotesLength = 2000;
    public const int MaxProtectedFieldLength = 2048;
}
