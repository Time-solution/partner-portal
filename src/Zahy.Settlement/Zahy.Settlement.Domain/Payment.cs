using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// A single inbound payment (money IN) applied against one AR position, identified by
/// <see cref="AgainstRef"/> (a settlement or invoice reference). A balance may receive SEVERAL
/// payments over time — partial then partial then final — so a payment is recorded per receipt,
/// never as a running total. The amount is stored gross (VAT-inclusive, as the AR carries it);
/// VAT is NOT re-split on a cash receipt. Recording a payment is config/record only — the matching
/// journal (Dr 1100 / Cr 1200|1250) is COMPUTED via <see cref="SettlementPostingTemplates.PaymentReceived"/>
/// and never posted while <c>SettlementEngineOptions.PostingEnabled</c> is OFF.
/// Mirrors the frontend mock Payment { id, againstRef, payer, payerId, amount, date, method? }.
/// </summary>
public class Payment : AggregateRoot<Guid>
{
    /// <summary>The settlement/invoice reference this payment is applied against.</summary>
    public string AgainstRef { get; private set; } = string.Empty;

    public PaymentPayer Payer { get; private set; }

    /// <summary>The merchant or partner id that paid (whichever <see cref="Payer"/> selects).</summary>
    public Guid PayerId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public DateTime Date { get; private set; }

    /// <summary>Optional free-text method (e.g. "bank-transfer", "mada"). Display only.</summary>
    public string? Method { get; private set; }

    /// <summary>The receipt as gross money (VAT-inclusive), matching the AR it clears.</summary>
    public Money Money => Money.Of(Amount, Currency, vatInclusive: true);

    protected Payment()
    {
    }

    private Payment(
        Guid id,
        string againstRef,
        PaymentPayer payer,
        Guid payerId,
        Money amount,
        DateTime date,
        string? method)
        : base(id)
    {
        AgainstRef = againstRef;
        Payer = payer;
        PayerId = payerId;
        Amount = amount.Amount;
        Currency = amount.Currency;
        Date = date;
        Method = method;
    }

    public static Payment Record(
        Guid id,
        string againstRef,
        PaymentPayer payer,
        Guid payerId,
        Money amount,
        DateTime date,
        string? method = null)
    {
        if (string.IsNullOrWhiteSpace(againstRef))
        {
            throw new BusinessException(SettlementPaymentErrorCodes.EmptyAgainstRef);
        }

        Check.NotNull(amount, nameof(amount));
        if (!amount.IsPositive)
        {
            throw new BusinessException(SettlementPaymentErrorCodes.NonPositivePayment)
                .WithData("Amount", amount.Amount);
        }

        return new Payment(id, againstRef.Trim(), payer, payerId, amount, date, method);
    }
}
