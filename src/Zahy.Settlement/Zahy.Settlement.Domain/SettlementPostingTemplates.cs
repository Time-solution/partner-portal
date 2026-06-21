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
    /// </summary>
    public static PostingResult Principal(Money sellInclusive, Money buyInclusive, decimal vatRate)
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
            PostingLine.Credit(SettlementAccountCode.ApPartner, Net(buyInclusive.Amount, currency)),
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
    /// </summary>
    public static PostingResult Fee(Money feeInclusive, ActivationFeePayer payer, decimal vatRate)
    {
        EnsureInclusive(feeInclusive, nameof(feeInclusive));

        var currency = feeInclusive.Currency;

        var feeNet = VatMath.NetOfInclusive(feeInclusive.Amount, vatRate);
        var outputVat = SettlementMoney.Round(feeInclusive.Amount - feeNet);

        var receivable = payer == ActivationFeePayer.Partner
            ? SettlementAccountCode.ArPartner   // 1250 — partner owes Zahy
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
    public static PostingResult PaymentReceived(Money amount, PaymentPayer payer)
    {
        Check.NotNull(amount, nameof(amount));
        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementPaymentErrorCodes.NonPositivePayment)
                .WithData("Amount", amount.Amount);
        }

        var currency = amount.Currency;

        var receivable = payer == PaymentPayer.Partner
            ? SettlementAccountCode.ArPartner   // 1250 — partner owed Zahy
            : SettlementAccountCode.ArMerchant; // 1200 — merchant owed Zahy

        var lines = new[]
        {
            PostingLine.Debit(SettlementAccountCode.BankCashClearing, Net(amount.Amount, currency)),
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
    /// </summary>
    public static PostingResult Disbursement(Money amount)
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
            PostingLine.Debit(SettlementAccountCode.ApPartner, Net(amount.Amount, currency)),
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
    /// trial balance returns to where it was. COMPUTE ONLY (flag OFF).
    /// </summary>
    public static PostingResult DisbursementReversal(Money amount)
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
            PostingLine.Credit(SettlementAccountCode.ApPartner, Net(amount.Amount, currency)),
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
