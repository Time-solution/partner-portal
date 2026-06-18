using Shouldly;
using Xunit;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerLifecyclePolicyTests
{
    [Theory]
    [InlineData(PartnerStatus.Pending, PartnerLifecycleAction.Approve, PartnerStatus.Active)]
    [InlineData(PartnerStatus.Pending, PartnerLifecycleAction.Reject, PartnerStatus.Closed)]
    [InlineData(PartnerStatus.Active, PartnerLifecycleAction.Suspend, PartnerStatus.Suspended)]
    [InlineData(PartnerStatus.Active, PartnerLifecycleAction.Close, PartnerStatus.Closed)]
    [InlineData(PartnerStatus.Suspended, PartnerLifecycleAction.Reactivate, PartnerStatus.Active)]
    [InlineData(PartnerStatus.Suspended, PartnerLifecycleAction.Close, PartnerStatus.Closed)]
    public void Should_Allow_Legal_Transitions(
        PartnerStatus current,
        PartnerLifecycleAction action,
        PartnerStatus expected)
    {
        PartnerLifecyclePolicy.TryGetTargetStatus(current, action, out var target).ShouldBeTrue();
        target.ShouldBe(expected);
    }

    [Theory]
    [InlineData(PartnerStatus.Pending, PartnerLifecycleAction.Suspend)]
    [InlineData(PartnerStatus.Pending, PartnerLifecycleAction.Reactivate)]
    [InlineData(PartnerStatus.Pending, PartnerLifecycleAction.Close)]
    [InlineData(PartnerStatus.Active, PartnerLifecycleAction.Approve)]
    [InlineData(PartnerStatus.Active, PartnerLifecycleAction.Reject)]
    [InlineData(PartnerStatus.Suspended, PartnerLifecycleAction.Approve)]
    [InlineData(PartnerStatus.Suspended, PartnerLifecycleAction.Reject)]
    [InlineData(PartnerStatus.Suspended, PartnerLifecycleAction.Suspend)]
    [InlineData(PartnerStatus.Closed, PartnerLifecycleAction.Approve)]
    [InlineData(PartnerStatus.Closed, PartnerLifecycleAction.Close)]
    public void Should_Reject_Illegal_Transitions(PartnerStatus current, PartnerLifecycleAction action)
    {
        PartnerLifecyclePolicy.TryGetTargetStatus(current, action, out _).ShouldBeFalse();
    }
}
