namespace Zahy.Commission;

public interface ICommissionCalculator
{
    CommissionCalculationResult Compute(
        decimal basisAmount,
        CommissionBasisDefinition basisDefinition,
        string currency = CommissionConsts.DefaultCurrency);

    CommissionCalculationResult ComputeForRule(
        decimal basisAmount,
        CommissionRuleSnapshot rule,
        string currency = CommissionConsts.DefaultCurrency);
}
