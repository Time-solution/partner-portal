using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// One leg of a code-based posting (posts against a real <see cref="SettlementChartOfAccounts"/>
/// code, not the legacy enum). The amount is always strictly positive; the sign is the direction.
/// </summary>
public sealed record PostingLine
{
    public string AccountCode { get; }

    public EntryDirection Direction { get; }

    public Money Amount { get; }

    private PostingLine(string accountCode, EntryDirection direction, Money amount)
    {
        AccountCode = accountCode;
        Direction = direction;
        Amount = amount;
    }

    public static PostingLine Of(string accountCode, EntryDirection direction, Money amount)
    {
        if (!SettlementAccountCode.IsPostable(accountCode))
        {
            throw new AbpException($"Unknown chart account code '{accountCode}'.");
        }

        if (amount is null || !amount.IsPositive)
        {
            throw new BusinessException(SettlementErrorCodes.NonPositiveAmount)
                .WithData("Account", accountCode)
                .WithData("Amount", amount?.Amount ?? 0m);
        }

        return new PostingLine(accountCode, direction, amount);
    }

    public static PostingLine Debit(string accountCode, Money amount) =>
        Of(accountCode, EntryDirection.Debit, amount);

    public static PostingLine Credit(string accountCode, Money amount) =>
        Of(accountCode, EntryDirection.Credit, amount);
}
