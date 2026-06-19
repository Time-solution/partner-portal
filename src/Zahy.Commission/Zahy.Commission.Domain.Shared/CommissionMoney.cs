namespace Zahy.Commission;

public static class CommissionMoney
{
    public const int IntermediateScale = 4;
    public const int CommissionScale = 2;

    public static decimal RoundIntermediate(decimal value) =>
        Math.Round(value, IntermediateScale, MidpointRounding.AwayFromZero);

    public static decimal RoundCommission(decimal value) =>
        Math.Round(value, CommissionScale, MidpointRounding.AwayFromZero);
}
