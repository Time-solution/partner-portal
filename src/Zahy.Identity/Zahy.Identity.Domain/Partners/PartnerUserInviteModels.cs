using System.Security.Claims;

namespace Zahy.Identity.Partners;

public class PartnerUserInviteRequest
{
    public Guid PartnerId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    /// <summary>When true (platform approve flow), skips interactive role-assignment policy.</summary>
    public bool BypassRoleAssignmentPolicy { get; set; }
}

public class PartnerUserInviteResult
{
    public Guid IdentityUserId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>One-time password reset token for set-password; never stored by Zahy.</summary>
    public string SetPasswordToken { get; set; } = string.Empty;
}

public interface IPartnerUserInviteService
{
    Task<PartnerUserInviteResult> InviteAsync(PartnerUserInviteRequest request);
}

public static class PartnerIdentityClaims
{
    public static Claim CreatePartnerIdClaim(Guid partnerId) =>
        new(ZahyClaimTypes.PartnerId, partnerId.ToString("D"));
}
