using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class BookIsolationGuardTests
{
    private static readonly DateTime T = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Journal_Using_Only_The_Books_Accounts_Passes()
    {
        var profile = new AggregatorFlowProfile();
        var journal = Journal.Create(Guid.NewGuid(), T, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.MerchantPayable, Money.Of(80m)),
            JournalLine.Credit(SettlementAccountType.PlatformCommissionRevenue, Money.Of(20m))
        });

        Should.NotThrow(() => BookIsolationGuard.EnsureWithinBook(profile, journal));
    }

    [Fact]
    public void Journal_Touching_A_Foreign_Account_Is_Rejected()
    {
        var profile = new AggregatorFlowProfile(); // does NOT own PartnerPayable
        var journal = Journal.Create(Guid.NewGuid(), T, new[]
        {
            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(100m)),
            JournalLine.Credit(SettlementAccountType.PartnerPayable, Money.Of(100m))
        });

        Should.Throw<BusinessException>(() => BookIsolationGuard.EnsureWithinBook(profile, journal))
            .Code.ShouldBe(SettlementCaseErrorCodes.AccountNotInBook);
    }
}
