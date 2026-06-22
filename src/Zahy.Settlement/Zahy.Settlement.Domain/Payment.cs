using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// How the receipt was tendered (Cash/Transfer/Card/COD/Online/Gateway). Null when unspecified.
    /// Persisted so a recorded receipt remembers its method (was previously free text).
    /// </summary>
    public PaymentMethod? Method { get; private set; }

    /// <summary>
    /// The bank sub-account (110x) the receipt landed in — the SAME destination the posting template
    /// routes the cash leg to (<see cref="SettlementPostingTemplates.PaymentReceived(Money, PaymentPayer, string?)"/>).
    /// Null means it falls back to the 1100 parent. Stored so "what posted" equals "what's recorded".
    /// </summary>
    public string? BankAccountCode { get; private set; }

    /// <summary>
    /// Double-submit safety: the same key resolves to ONE receipt (unique in persistence). When a caller
    /// supplies no key, it defaults to the row id, so legacy single-shot records never collide while an
    /// explicit key lets a replay be detected as a no-op (see the guarded <see cref="Record"/> overload).
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

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
        PaymentMethod? method,
        string? bankAccountCode,
        string idempotencyKey)
        : base(id)
    {
        AgainstRef = againstRef;
        Payer = payer;
        PayerId = payerId;
        Amount = amount.Amount;
        Currency = amount.Currency;
        Date = date;
        Method = method;
        BankAccountCode = bankAccountCode;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Low-level factory: validates the ref + amount, normalises the bank destination (a non-110x code
    /// collapses to null → the 1100 fallback, matching the posting template) and stamps an idempotency
    /// key (defaulting to the row id when none is supplied). Use the guarded overload below to ALSO reject
    /// overpayment and dedupe a double-submit; this one is the raw constructor used e.g. to rebuild
    /// persisted receipts.
    /// </summary>
    public static Payment Record(
        Guid id,
        string againstRef,
        PaymentPayer payer,
        Guid payerId,
        Money amount,
        DateTime date,
        PaymentMethod? method = null,
        string? bankAccountCode = null,
        string? idempotencyKey = null)
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

        return new Payment(id, againstRef.Trim(), payer, payerId, amount, date, method,
            NormalizeBankCode(bankAccountCode), ResolveIdempotencyKey(idempotencyKey, id));
    }

    /// <summary>
    /// Guarded recording — the overpayment + idempotency rules are enforced HERE at the domain, never left
    /// to the caller:
    ///   (1) <see cref="PaymentLedger.EnsureWithinRemaining"/> rejects a receipt that would push cumulative
    ///       receipts past the AR total (overpayment).
    ///   (2) a double-submit carrying the same idempotency key is a NO-OP — the existing receipt is returned
    ///       and no duplicate row is created (the unique index is the persistence-level backstop).
    /// <paramref name="existingForRef"/> is the set of receipts already recorded for this AR ref.
    /// </summary>
    public static Payment Record(
        Guid id,
        string againstRef,
        PaymentPayer payer,
        Guid payerId,
        Money amount,
        DateTime date,
        Money arTotal,
        IEnumerable<Payment> existingForRef,
        PaymentMethod? method = null,
        string? bankAccountCode = null,
        string? idempotencyKey = null)
    {
        Check.NotNull(arTotal, nameof(arTotal));
        var rows = existingForRef?.ToList() ?? new List<Payment>();
        var key = ResolveIdempotencyKey(idempotencyKey, id);

        // (2) Idempotency: a replayed submit with the same key resolves to the SAME receipt.
        var duplicate = rows.FirstOrDefault(p =>
            string.Equals(p.IdempotencyKey, key, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            return duplicate;
        }

        // (1) Overpayment guard — invoked at the domain, not deferred to the caller.
        PaymentLedger.EnsureWithinRemaining(arTotal, PaymentLedger.PaidToDate(againstRef, rows), amount);

        return Record(id, againstRef, payer, payerId, amount, date, method, bankAccountCode, key);
    }

    private static string ResolveIdempotencyKey(string? idempotencyKey, Guid id) =>
        string.IsNullOrWhiteSpace(idempotencyKey) ? id.ToString("N") : idempotencyKey.Trim();

    /// <summary>
    /// Keep only a real bank sub-account (110x) — anything else collapses to null so the recorded
    /// destination matches the posting template's own 1100 fallback. The stored code therefore always
    /// equals the code the cash leg debited.
    /// </summary>
    private static string? NormalizeBankCode(string? bankAccountCode) =>
        BankLedgerCoding.IsBankSubAccount(bankAccountCode) ? bankAccountCode!.Trim() : null;
}
