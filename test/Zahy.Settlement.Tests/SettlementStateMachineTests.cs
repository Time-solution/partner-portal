using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementStateMachineTests
{
    [Theory]
    [InlineData(SettlementCaseState.Collected, SettlementCaseState.Allocated, true)]
    [InlineData(SettlementCaseState.Allocated, SettlementCaseState.Invoiced, true)]
    [InlineData(SettlementCaseState.Invoiced, SettlementCaseState.Cleared, true)]
    [InlineData(SettlementCaseState.Cleared, SettlementCaseState.Disbursed, true)]
    [InlineData(SettlementCaseState.Disbursed, SettlementCaseState.Reconciled, true)]
    [InlineData(SettlementCaseState.Collected, SettlementCaseState.Invoiced, false)] // skip
    [InlineData(SettlementCaseState.Allocated, SettlementCaseState.Collected, false)] // backward
    [InlineData(SettlementCaseState.Cleared, SettlementCaseState.Cleared, false)] // self
    [InlineData(SettlementCaseState.Reconciled, SettlementCaseState.Disbursed, false)] // terminal
    public void CanTransition_Allows_Only_Linear_Forward_Steps(
        SettlementCaseState from,
        SettlementCaseState to,
        bool expected)
    {
        SettlementStateMachine.CanTransition(from, to).ShouldBe(expected);
    }

    [Fact]
    public void NextOf_Walks_The_Chain_And_Terminates_At_Reconciled()
    {
        SettlementStateMachine.NextOf(SettlementCaseState.Collected).ShouldBe(SettlementCaseState.Allocated);
        SettlementStateMachine.NextOf(SettlementCaseState.Disbursed).ShouldBe(SettlementCaseState.Reconciled);
        SettlementStateMachine.NextOf(SettlementCaseState.Reconciled).ShouldBeNull();
        SettlementStateMachine.IsTerminal(SettlementCaseState.Reconciled).ShouldBeTrue();
        SettlementStateMachine.IsTerminal(SettlementCaseState.Cleared).ShouldBeFalse();
    }
}
