using Zahy.Commission;

namespace Zahy.Finance;

public static class FinancePostingSignMapper
{
    public static decimal MapCommissionAmount(CommissionDirection direction, decimal computedCommission) =>
        direction switch
        {
            CommissionDirection.PlatformEarns => FinanceMoney.RoundPosting(computedCommission),
            CommissionDirection.PartnerEarns => FinanceMoney.RoundPosting(-computedCommission),
            _ => throw new Volo.Abp.BusinessException(FinanceErrorCodes.InvalidPosting)
                .WithData("Direction", direction.ToString())
        };

    public static decimal MapBillingChargeAmount(decimal amount) =>
        FinanceMoney.RoundPosting(amount);
}
