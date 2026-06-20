using Shouldly;
using Xunit;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogItemStateMachineTests
{
    [Theory]
    [InlineData(PartnerCatalogItemStatus.Draft, PartnerCatalogItemStatus.Active, true)]
    [InlineData(PartnerCatalogItemStatus.Draft, PartnerCatalogItemStatus.Archived, true)]
    [InlineData(PartnerCatalogItemStatus.Active, PartnerCatalogItemStatus.Archived, true)]
    [InlineData(PartnerCatalogItemStatus.Active, PartnerCatalogItemStatus.Draft, false)]
    [InlineData(PartnerCatalogItemStatus.Archived, PartnerCatalogItemStatus.Active, false)]
    public void CanTransition_Matches_Design(
        PartnerCatalogItemStatus from,
        PartnerCatalogItemStatus to,
        bool expected)
    {
        PartnerCatalogItemStateMachine.CanTransition(from, to).ShouldBe(expected);
    }
}
