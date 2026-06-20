using System;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// Money value object: amount + ISO-4217 currency + a VAT-inclusive marker.
/// Amounts are rounded to 2dp away-from-zero at construction (round-per-line policy, CTO-approved).
/// Arithmetic is only allowed between compatible money (same currency AND same VAT-inclusive flag),
/// which prevents accidentally mixing gross and net figures.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    public bool VatInclusive { get; }

    private Money(decimal amount, string currency, bool vatInclusive)
    {
        Amount = amount;
        Currency = currency;
        VatInclusive = vatInclusive;
    }

    public static Money Of(
        decimal amount,
        string currency = SettlementConsts.DefaultCurrency,
        bool vatInclusive = false) =>
        new(SettlementMoney.Round(amount), NormalizeCurrency(currency), vatInclusive);

    public static Money Zero(
        string currency = SettlementConsts.DefaultCurrency,
        bool vatInclusive = false) =>
        new(0m, NormalizeCurrency(currency), vatInclusive);

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    public Money Abs() => new(Math.Abs(Amount), Currency, VatInclusive);

    public static Money operator +(Money left, Money right)
    {
        GuardCompatible(left, right);
        return new Money(SettlementMoney.Round(left.Amount + right.Amount), left.Currency, left.VatInclusive);
    }

    public static Money operator -(Money left, Money right)
    {
        GuardCompatible(left, right);
        return new Money(SettlementMoney.Round(left.Amount - right.Amount), left.Currency, left.VatInclusive);
    }

    public static Money operator -(Money value) =>
        new(SettlementMoney.Round(-value.Amount), value.Currency, value.VatInclusive);

    private static void GuardCompatible(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new BusinessException(SettlementErrorCodes.CurrencyMismatch)
                .WithData("Left", left.Currency)
                .WithData("Right", right.Currency);
        }

        if (left.VatInclusive != right.VatInclusive)
        {
            throw new BusinessException(SettlementErrorCodes.VatInclusiveMismatch)
                .WithData("Left", left.VatInclusive)
                .WithData("Right", right.VatInclusive);
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new BusinessException(SettlementErrorCodes.InvalidCurrency)
                .WithData("Currency", currency ?? "<null>");
        }

        var trimmed = currency.Trim();
        if (trimmed.Length != 3 || !IsAllLetters(trimmed))
        {
            throw new BusinessException(SettlementErrorCodes.InvalidCurrency)
                .WithData("Currency", currency);
        }

        return trimmed.ToUpperInvariant();
    }

    private static bool IsAllLetters(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString() =>
        $"{Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {Currency}{(VatInclusive ? " (incl)" : string.Empty)}";
}
