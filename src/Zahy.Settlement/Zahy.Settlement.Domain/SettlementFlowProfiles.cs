using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>Shared base data for a flow profile — holds the account-tree set only, NOT behaviour. Concrete
/// profiles vary by data (their book + account tree), and the engine composes them via the resolver.</summary>
public abstract class FlowProfileBase : ISettlementFlowProfile
{
    private readonly HashSet<SettlementAccountType> _accountTree;

    protected FlowProfileBase(SettlementBook book, IEnumerable<SettlementAccountType> accountTree)
    {
        Book = book;
        _accountTree = new HashSet<SettlementAccountType>(accountTree);
    }

    public SettlementBook Book { get; }

    public IReadOnlyCollection<SettlementAccountType> AccountTree => _accountTree;

    public bool Owns(SettlementAccountType account) => _accountTree.Contains(account);
}

/// <summary>
/// Marketplace / Aggregator (Model 2): collected total is split into a merchant payout, delivery
/// cost, platform commission and aggregator clearing, with VAT. Account tree is provisional pending
/// the accountant's chart sign-off (DESIGN.md §11.6).
/// </summary>
public sealed class AggregatorFlowProfile : FlowProfileBase
{
    public AggregatorFlowProfile()
        : base(SettlementBook.Marketplace, new[]
        {
            SettlementAccountType.AggregatorClearing,
            SettlementAccountType.MerchantPayable,
            SettlementAccountType.DeliveryCost,
            SettlementAccountType.PlatformCommissionRevenue,
            SettlementAccountType.ShippingMarginRevenue,
            SettlementAccountType.VatOutput,
            SettlementAccountType.VatInput
        })
    {
    }
}

/// <summary>
/// Integration / Service (Model 3): cost + markup resale — partner payout, shipping-margin revenue,
/// platform commission, with VAT. Account tree is provisional pending chart sign-off (DESIGN.md §11.6).
/// </summary>
public sealed class ServiceFlowProfile : FlowProfileBase
{
    public ServiceFlowProfile()
        : base(SettlementBook.Integration, new[]
        {
            SettlementAccountType.PartnerPayable,
            SettlementAccountType.DeliveryCost,
            SettlementAccountType.ShippingMarginRevenue,
            SettlementAccountType.PlatformCommissionRevenue,
            SettlementAccountType.VatOutput,
            SettlementAccountType.VatInput
        })
    {
    }
}

public sealed class SettlementFlowProfileResolver : ISettlementFlowProfileResolver
{
    private readonly IReadOnlyDictionary<SettlementBook, ISettlementFlowProfile> _profiles;

    public SettlementFlowProfileResolver(IEnumerable<ISettlementFlowProfile> profiles)
    {
        _profiles = profiles.ToDictionary(p => p.Book);
    }

    public ISettlementFlowProfile Resolve(SettlementBook book) =>
        _profiles.TryGetValue(book, out var profile)
            ? profile
            : throw new BusinessException(SettlementCaseErrorCodes.UnknownFlowProfile)
                .WithData("Book", book.ToString());
}
