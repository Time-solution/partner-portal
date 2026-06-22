using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Posting templates bound to the real chart codes (1100-5100) for the three participation modes.
/// VAT back-out is round-per-line (2dp final, AwayFromZero) via <see cref="VatMath"/>. Margin and
/// net VAT are DERIVED summaries on the result — never journal lines that would inflate the total.
/// </summary>
public static class SettlementPostingTemplates
{
    /// <summary>
    /// PRINCIPAL (buy incl VAT, sell incl VAT):
    ///   Dr 1200 AR-Merchant = sell incl; Cr 4100 Resale Revenue = sell ex-VAT; Cr 2200 Output VAT.
    ///   Dr 5100 COGS = buy ex-VAT; Dr 1300 Input VAT; Cr 2100 AP-Partner = buy incl.
    /// When <paramref name="partnerApCode"/> is a partner payable sub-account (2101–2149) the AP credit
    /// routes to that partner's own ledger line instead of the 2100 parent; the sub-account rolls up to
    /// 2100, so the trial balance is unaffected. Null/blank ⇒ 2100 parent (back-compat).
    /// </summary>
    public static PostingResult Principal(Money sellInclusive, Money buyInclusive, decimal vatRate, string? partnerApCode = null)
    {
        EnsureInclusive(sellInclusive, nameof(sellInclusive));
        EnsureInclusive(buyInclusive, nameof(buyInclusive));
        EnsureSameCurrency(sellInclusive, buyInclusive);

        var currency = sellInclusive.Currency;

        var sellNet = VatMath.NetOfInclusive(sellInclusive.Amount, vatRate);
        var outputVat = SettlementMoney.Round(sellInclusive.Amount - sellNet);
        var buyNet = VatMath.NetOfInclusive(buyInclusive.Amount, vatRate);
        var inputVat = SettlementMoney.Round(buyInclusive.Amount - buyNet);

        var margin = SettlementMoney.Round(sellNet - buyNet);
        var netVat = SettlementMoney.Round(outputVat - inputVat);

        var lines = new[]
        {
            // Sell side — totals sell incl.
            PostingLine.Debit(SettlementAccountCode.ArMerchant, Net(sellInclusive.Amount, currency)),
            PostingLine.Credit(SettlementAccountCode.ResaleRevenue, Net(sellNet, currency)),
            PostingLine.Credit(SettlementAccountCode.OutputVat, Net(outputVat, currency)),
            // Buy side — totals buy incl.
            PostingLine.Debit(SettlementAccountCode.PartnerCogs, Net(buyNet, currency)),
            PostingLine.Debit(SettlementAccountCode.InputVat, Net(inputVat, currency)),
            PostingLine.Credit(RouteApPartner(partnerApCode), Net(buyInclusive.Amount, currency)),
        };

        return PostingResult.Financial(
            ParticipationMode.Principal,
            lines,
            Money.Of(margin, currency),
            Money.Of(netVat, currency));
    }

    /// <summary>
    /// SUBSCRIPTIONFEE (fee incl VAT), always billed to the merchant (1200):
    ///   Dr 1200 AR-Merchant = fee incl; Cr 4200 Fee Revenue = fee ex-VAT; Cr 2200 Output VAT.
    /// No cost leg (no 5100, no 1300). Thin wrapper over <see cref="Fee"/> with payer = Merchant.
    /// </summary>
    public static PostingResult SubscriptionFee(Money feeInclusive, decimal vatRate) =>
        Fee(feeInclusive, ActivationFeePayer.Merchant, vatRate);

    /// <summary>
    /// FEE (activation fee, incl VAT) — payer-selectable receivable, reuses the Phase B fee template:
    ///   Payer = Merchant → Dr 1200 AR-Merchant; Payer = Partner → Dr 1250 AR-Partner.
    ///   both → Cr 4200 Fee Revenue (ex-VAT) + Cr 2200 Output VAT.
    /// No cost leg (no 5100, no 1300). VAT 15%, round-per-line. COMPUTE ONLY — callers must not
    /// persist/post this while <c>SettlementEngineOptions.PostingEnabled</c> is OFF.
    /// When the payer is the Partner and <paramref name="partnerArCode"/> is a partner receivable
    /// sub-account (1251–1299) the AR debit routes to that partner's own ledger line instead of the 1250
    /// parent (rolls up to 1250 — trial balance unaffected). Null/blank ⇒ 1250 parent (back-compat).
    /// </summary>
    public static PostingResult Fee(Money feeInclusive, ActivationFeePayer payer, decimal vatRate, string? partnerArCode = null)
    {
        EnsureInclusive(feeInclusive, nameof(feeInclusive));

        var currency = feeInclusive.Currency;

        var feeNet = VatMath.NetOfInclusive(feeInclusive.Amount, vatRate);
        var outputVat = SettlementMoney.Round(feeInclusive.Amount - feeNet);

        var receivable = payer == ActivationFeePayer.Partner
            ? RouteArPartner(partnerArCode)     // 1250 parent or the partner's 1251+ sub-account
            : SettlementAccountCode.ArMerchant; // 1200 — merchant owes Zahy

        var lines = new[]
        {
            PostingLine.Debit(receivable, Net(feeInclusive.Amount, currency)),
            PostingLine.Credit(SettlementAccountCode.FeeRevenue, Net(feeNet, currency)),
            PostingLine.Credit(SettlementAccountCode.OutputVat, Net(outputVat, currency)),
        };

        // No buy leg: the whole net fee is the platform's margin; net VAT = output VAT.
        return PostingResult.Financial(
            ParticipationMode.SubscriptionFee,
            lines,
            Money.Of(feeNet, currency),
            Money.Of(outputVat, currency));
    }

    /// <summary>
    /// PAYMENTRECEIVED (money IN) — an inbound cash receipt clearing a receivable for its full amount:
    ///   Payer = Merchant → Dr 1100 Bank/Cash; Cr 1200 AR-Merchant.
    ///   Payer = Partner  → Dr 1100 Bank/Cash; Cr 1250 AR-Partner.
    /// No VAT re-split (VAT was recognised when the AR was booked); touches no revenue/VAT/COGS, so
    /// margin and net VAT are zero — it only nets the AR down. Balanced for the exact <paramref name="amount"/>.
    /// COMPUTE ONLY — callers must not persist/post while <c>SettlementEngineOptions.PostingEnabled</c> is OFF.
    /// </summary>
    public static PostingResult PaymentReceived(Money amount, PaymentPayer payer) =>
        PaymentReceived(amount, payer, bankAccountCode: null);

    /// <summary>
    /// PAYMENTRECEIVED with MANUAL BANK ROUTING (Track B) and optional per-partner AR routing. The cash
    /// leg debits the chosen bank's 110x sub-account instead of the generic 1100 parent, and a partner
    /// receipt may credit that partner's 1251+ sub-account instead of the 1250 parent:
    ///   Dr {bankAccountCode ?? 1100} Bank/Cash; Cr {partnerArCode ?? 1200/1250} AR.
    /// Both sub-accounts roll up to their parents, so the trial balance is unaffected. Null/blank ⇒
    /// parent fallback (back-compat). COMPUTE ONLY (flag OFF).
    /// </summary>
    public static PostingResult PaymentReceived(Money amount, PaymentPayer payer, string? bankAccountCode, string? partnerArCode = null)
    {
        Check.NotNull(amount, nameof(amount));
        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementPaymentErrorCodes.NonPositivePayment)
                .WithData("Amount", amount.Amount);
        }

        var currency = amount.Currency;

        var receivable = payer == PaymentPayer.Partner
            ? RouteArPartner(partnerArCode)     // 1250 parent or the partner's 1251+ sub-account
            : SettlementAccountCode.ArMerchant; // 1200 — merchant owed Zahy

        // Manual routing: a chosen bank sub-account (110x) takes the cash; otherwise the 1100 parent.
        var bankCode = BankLedgerCoding.IsBankSubAccount(bankAccountCode)
            ? bankAccountCode!
            : SettlementAccountCode.BankCashClearing;

        var lines = new[]
        {
            PostingLine.Debit(bankCode, Net(amount.Amount, currency)),
            PostingLine.Credit(receivable, Net(amount.Amount, currency)),
        };

        // A cash receipt earns no margin and recognises no VAT — both derived summaries are zero.
        return PostingResult.Financial(
            ParticipationMode.PaymentReceived,
            lines,
            Money.Zero(currency),
            Money.Zero(currency));
    }

    /// <summary>
    /// DISBURSEMENT (money OUT) — an outbound payout to the partner clearing the payable:
    ///   Dr 2100 AP-Partner; Cr 1100 Bank/Cash.
    /// Touches no revenue/VAT/COGS, so margin and net VAT are zero — it only nets the payable down.
    /// COMPUTE ONLY — callers must not release/post while <c>SettlementEngineOptions.DisbursementEnabled</c>
    /// is OFF, and a Locked disbursement computes no journal regardless.
    /// When <paramref name="partnerApCode"/> is a partner payable sub-account (2101–2149) the AP debit
    /// routes to that partner's own ledger line instead of the 2100 parent (rolls up to 2100 — trial
    /// balance unaffected). Null/blank ⇒ 2100 parent (back-compat).
    /// </summary>
    public static PostingResult Disbursement(Money amount, string? partnerApCode = null)
    {
        Check.NotNull(amount, nameof(amount));
        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.NonPositiveDisbursement)
                .WithData("Amount", amount.Amount);
        }

        var currency = amount.Currency;

        var lines = new[]
        {
            PostingLine.Debit(RouteApPartner(partnerApCode), Net(amount.Amount, currency)),
            PostingLine.Credit(SettlementAccountCode.BankCashClearing, Net(amount.Amount, currency)),
        };

        return PostingResult.Financial(
            ParticipationMode.Disbursement,
            lines,
            Money.Zero(currency),
            Money.Zero(currency));
    }

    /// <summary>
    /// DISBURSEMENT REVERSAL (correction) — the inverse of a disbursement, posted as a NEW append-only
    /// row (never an edit): Dr 1100 Bank/Cash; Cr 2100 AP-Partner. Nets the original payout back so the
    /// trial balance returns to where it was. The AP credit routes to the same partner sub-account the
    /// original disbursement debited when <paramref name="partnerApCode"/> is supplied. COMPUTE ONLY (flag OFF).
    /// </summary>
    public static PostingResult DisbursementReversal(Money amount, string? partnerApCode = null)
    {
        Check.NotNull(amount, nameof(amount));
        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementDisbursementErrorCodes.NonPositiveDisbursement)
                .WithData("Amount", amount.Amount);
        }

        var currency = amount.Currency;

        var lines = new[]
        {
            PostingLine.Debit(SettlementAccountCode.BankCashClearing, Net(amount.Amount, currency)),
            PostingLine.Credit(RouteApPartner(partnerApCode), Net(amount.Amount, currency)),
        };

        return PostingResult.Financial(
            ParticipationMode.Disbursement,
            lines,
            Money.Zero(currency),
            Money.Zero(currency));
    }

    /// <summary>REFLECTIONONLY: no financial journal at all (a reflection log is written separately).</summary>
    public static PostingResult ReflectionOnly(string currency = SettlementConsts.DefaultCurrency) =>
        PostingResult.NonPosting(ParticipationMode.ReflectionOnly, currency);

    private static Money Net(decimal amount, string currency) => Money.Of(amount, currency);

    /// <summary>The 2100 parent, or the partner's 2101+ payable sub-account when a valid one is given.</summary>
    private static string RouteApPartner(string? partnerApCode) =>
        PartnerLedgerCoding.IsPayableSubAccount(partnerApCode)
            ? partnerApCode!.Trim()
            : SettlementAccountCode.ApPartner;

    /// <summary>The 1250 parent, or the partner's 1251+ receivable sub-account when a valid one is given.</summary>
    private static string RouteArPartner(string? partnerArCode) =>
        PartnerLedgerCoding.IsReceivableSubAccount(partnerArCode)
            ? partnerArCode!.Trim()
            : SettlementAccountCode.ArPartner;

    private static void EnsureInclusive(Money money, string name)
    {
        Check.NotNull(money, name);
        if (!money.VatInclusive)
        {
            throw new BusinessException(SettlementVatErrorCodes.PriceMustBeVatInclusive)
                .WithData("Argument", name);
        }
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, System.StringComparison.Ordinal))
        {
            throw new BusinessException(SettlementVatErrorCodes.PriceCurrencyMismatch)
                .WithData("Buy", b.Currency)
                .WithData("Sell", a.Currency);
        }
    }
}
