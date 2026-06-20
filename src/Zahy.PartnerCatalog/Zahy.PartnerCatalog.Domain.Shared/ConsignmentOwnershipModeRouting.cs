namespace Zahy.PartnerCatalog;

/// <summary>
/// The single new decision for JUMP / Consignment-Fulfilment: route the configured
/// <see cref="ConsignmentOwnershipMode"/> to an EXISTING <see cref="SettlementParticipationMode"/>.
/// No new settlement logic — MerchantOwned reuses ReflectionOnly, PartnerBought reuses Principal.
/// Default is <see cref="ConsignmentOwnershipMode.MerchantOwned"/> (true consignment).
/// Final mode is confirmed with the accountant at go-live, like other flows.
/// </summary>
public static class ConsignmentOwnershipModeRouting
{
    public const ConsignmentOwnershipMode DefaultMode = ConsignmentOwnershipMode.MerchantOwned;

    public static SettlementParticipationMode ResolveParticipationMode(ConsignmentOwnershipMode ownershipMode) =>
        ownershipMode switch
        {
            ConsignmentOwnershipMode.MerchantOwned => SettlementParticipationMode.ReflectionOnly,
            ConsignmentOwnershipMode.PartnerBought => SettlementParticipationMode.Principal,
            _ => SettlementParticipationMode.ReflectionOnly
        };

    /// <summary>Null-safe overload — a missing setting defaults to consignment (ReflectionOnly).</summary>
    public static SettlementParticipationMode ResolveParticipationMode(ConsignmentOwnershipMode? ownershipMode) =>
        ResolveParticipationMode(ownershipMode ?? DefaultMode);
}
