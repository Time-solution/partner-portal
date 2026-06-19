using System.Linq;
using Volo.Abp;

namespace Zahy.Commission;

public class CommissionCalculator : ICommissionCalculator
{
    public CommissionCalculationResult Compute(
        decimal basisAmount,
        CommissionBasisDefinition basisDefinition,
        string currency = CommissionConsts.DefaultCurrency)
    {
        Check.NotNull(basisDefinition, nameof(basisDefinition));
        ValidateBasisAmount(basisAmount);
        ValidateCurrency(currency);

        var componentTotal = ComputeComponentTotal(basisAmount, basisDefinition);
        var constrained = ApplyConstraints(componentTotal, basisDefinition);
        var finalCommission = CommissionMoney.RoundCommission(Math.Max(0m, constrained));

        return new CommissionCalculationResult
        {
            BasisAmount = basisAmount,
            RawComponentTotal = componentTotal,
            ComputedCommission = finalCommission,
            Currency = currency.Trim().ToUpperInvariant()
        };
    }

    public CommissionCalculationResult ComputeForRule(
        decimal basisAmount,
        CommissionRuleSnapshot rule,
        string currency = CommissionConsts.DefaultCurrency)
    {
        Check.NotNull(rule, nameof(rule));
        return Compute(basisAmount, rule.BasisDefinition, currency);
    }

    private static void ValidateBasisAmount(decimal basisAmount)
    {
        if (basisAmount < 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisAmount);
        }
    }

    private static void ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidCurrency);
        }
    }

    private static decimal ComputeComponentTotal(decimal basisAmount, CommissionBasisDefinition basisDefinition)
    {
        decimal total = 0m;

        if (basisDefinition.FlatFee.HasValue)
        {
            total += basisDefinition.FlatFee.Value;
        }

        if (basisDefinition.PercentageRate.HasValue)
        {
            total += CommissionMoney.RoundIntermediate(basisAmount * basisDefinition.PercentageRate.Value);
        }

        if (basisDefinition.Tiered != null)
        {
            total += ComputeTiered(basisAmount, basisDefinition.Tiered);
        }

        return CommissionMoney.RoundIntermediate(total);
    }

    private static decimal ComputeTiered(decimal basisAmount, TieredCommissionDefinition tiered)
    {
        if (tiered.Mode != TieredCommissionMode.Bracket)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisDefinition)
                .WithData("TieredMode", tiered.Mode.ToString());
        }

        var tier = tiered.Tiers
            .Where(x => basisAmount >= x.FromAmount && (x.ToAmount == null || basisAmount <= x.ToAmount))
            .OrderByDescending(x => x.FromAmount)
            .FirstOrDefault();

        if (tier == null)
        {
            return 0m;
        }

        decimal total = 0m;
        if (tier.FlatFee.HasValue)
        {
            total += tier.FlatFee.Value;
        }

        if (tier.Rate.HasValue)
        {
            total += basisAmount * tier.Rate.Value;
        }

        return CommissionMoney.RoundIntermediate(total);
    }

    private static decimal ApplyConstraints(decimal componentTotal, CommissionBasisDefinition basisDefinition)
    {
        var result = componentTotal;

        if (basisDefinition.MinCommission.HasValue)
        {
            result = Math.Max(result, basisDefinition.MinCommission.Value);
        }

        if (basisDefinition.MaxCommission.HasValue)
        {
            result = Math.Min(result, basisDefinition.MaxCommission.Value);
        }

        if (basisDefinition.CapCommission.HasValue)
        {
            result = Math.Min(result, basisDefinition.CapCommission.Value);
        }

        return result;
    }
}
