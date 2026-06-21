using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// A chart-of-accounts line: a stable account <see cref="Code"/>, human <see cref="Name"/>, its
/// <see cref="LedgerAccountType"/> classification, the <see cref="NormalSide"/> (debit/credit-normal)
/// and the partner-type <see cref="Portfolio"/> it is scoped to. Codes are unique across the chart.
/// </summary>
public class LedgerAccount : AggregateRoot<Guid>
{
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public LedgerAccountType Type { get; private set; }

    public EntryDirection NormalSide { get; private set; }

    public AccountPortfolio Portfolio { get; private set; }

    protected LedgerAccount()
    {
    }

    public LedgerAccount(
        Guid id,
        string code,
        string name,
        LedgerAccountType type,
        EntryDirection normalSide,
        AccountPortfolio portfolio)
        : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), SettlementLedgerAccountConsts.MaxCodeLength).Trim();
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), SettlementLedgerAccountConsts.MaxNameLength).Trim();
        Type = type;
        NormalSide = normalSide;
        Portfolio = portfolio;
    }
}
