using Shouldly;
using Xunit;

namespace Zahy.PartnerCatalog;

public class MerchantActivationStateMachineTests
{
    [Theory]
    [InlineData(MerchantActivationStatus.Pending, MerchantActivationStatus.Active, true)]
    [InlineData(MerchantActivationStatus.Pending, MerchantActivationStatus.Ended, true)]
    [InlineData(MerchantActivationStatus.Active, MerchantActivationStatus.Suspended, true)]
    [InlineData(MerchantActivationStatus.Active, MerchantActivationStatus.Ended, true)]
    [InlineData(MerchantActivationStatus.Suspended, MerchantActivationStatus.Active, true)]
    [InlineData(MerchantActivationStatus.Suspended, MerchantActivationStatus.Ended, true)]
    [InlineData(MerchantActivationStatus.Pending, MerchantActivationStatus.Suspended, false)]
    [InlineData(MerchantActivationStatus.Ended, MerchantActivationStatus.Active, false)]
    [InlineData(MerchantActivationStatus.Ended, MerchantActivationStatus.Suspended, false)]
    [InlineData(MerchantActivationStatus.Active, MerchantActivationStatus.Pending, false)]
    public void CanTransition_Matches_Design(
        MerchantActivationStatus from,
        MerchantActivationStatus to,
        bool expected)
    {
        MerchantActivationStateMachine.CanTransition(from, to).ShouldBe(expected);
    }

    [Fact]
    public void Ended_Is_Terminal()
    {
        MerchantActivationStateMachine.IsTerminal(MerchantActivationStatus.Ended).ShouldBeTrue();
        MerchantActivationStateMachine.IsTerminal(MerchantActivationStatus.Active).ShouldBeFalse();
    }
}
