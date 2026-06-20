using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// One leg of a double-entry journal. The amount is always strictly positive; the sign of the
/// movement is expressed by <see cref="EntryDirection"/>.
/// </summary>
public sealed record JournalLine
{
    public SettlementAccountType Account { get; }

    public EntryDirection Direction { get; }

    public Money Amount { get; }

    private JournalLine(SettlementAccountType account, EntryDirection direction, Money amount)
    {
        Account = account;
        Direction = direction;
        Amount = amount;
    }

    public static JournalLine Of(SettlementAccountType account, EntryDirection direction, Money amount)
    {
        if (amount is null || !amount.IsPositive)
        {
            throw new BusinessException(SettlementErrorCodes.NonPositiveAmount)
                .WithData("Account", account.ToString())
                .WithData("Amount", amount?.Amount ?? 0m);
        }

        return new JournalLine(account, direction, amount);
    }

    public static JournalLine Debit(SettlementAccountType account, Money amount) =>
        Of(account, EntryDirection.Debit, amount);

    public static JournalLine Credit(SettlementAccountType account, Money amount) =>
        Of(account, EntryDirection.Credit, amount);
}
