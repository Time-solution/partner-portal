namespace Zahy.Settlement;

/// <summary>
/// Field limits + defaults for the U1 usage-metering record. Usage is PURE DATA: it records how many
/// units a merchant consumed for a given partner in a given period (e.g. message count). It NEVER
/// bills or posts a journal — pricing/posting is the separately-gated U3 phase.
/// </summary>
public static class SettlementUsageConsts
{
    /// <summary>The consumed unit, e.g. "messages", "api-calls", "units".</summary>
    public const int MaxUnitLabelLength = 32;

    /// <summary>Where the record came from, e.g. "seed", "manual", "partner-feed".</summary>
    public const int MaxSourceLength = 64;

    public const string DefaultUnitLabel = "units";

    public const string DefaultSource = "manual";
}
