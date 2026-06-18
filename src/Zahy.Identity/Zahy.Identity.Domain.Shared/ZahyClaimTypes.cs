namespace Zahy.Identity;

/// <summary>
/// Custom claim types issued by the Zahy IdP. Merchant isolation uses ABP's
/// tenant id; partner aggregates are host-level, so partner membership is
/// carried explicitly as a claim and enforced by the role-assignment policy.
/// </summary>
public static class ZahyClaimTypes
{
    /// <summary>Id of the partner aggregate the user belongs to (null for platform/merchant users).</summary>
    public const string PartnerId = "partner_id";
}
