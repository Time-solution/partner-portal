namespace Zahy.Settlement;

public static class SettlementLedgerAccountConsts
{
    /// <summary>Account code, e.g. "1200". Short, stable, unique across the chart.</summary>
    public const int MaxCodeLength = 16;

    public const int MaxNameLength = 128;
}

public static class SettlementReflectionLogConsts
{
    public const int MaxOrderReferenceLength = 128;
}
