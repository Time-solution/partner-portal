using Shouldly;
using Xunit;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinancePostingSignMapperTests
{
    [Theory]
    [InlineData(CommissionDirection.PlatformEarns, 10.00, 10.00)]
    [InlineData(CommissionDirection.PartnerEarns, 5.00, -5.00)]
    public void MapCommissionAmount_Uses_Platform_Plus_Partner_Minus(
        CommissionDirection direction,
        decimal computed,
        decimal expected)
    {
        FinancePostingSignMapper.MapCommissionAmount(direction, computed).ShouldBe(expected);
    }

    [Fact]
    public void MapBillingChargeAmount_Is_Positive_On_Target_Account()
    {
        FinancePostingSignMapper.MapBillingChargeAmount(49m).ShouldBe(49m);
    }
}
