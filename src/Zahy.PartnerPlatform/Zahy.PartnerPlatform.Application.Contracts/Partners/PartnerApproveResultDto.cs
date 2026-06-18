using System.Collections.Generic;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerApproveResultDto
{
    public PartnerDto Partner { get; set; } = new();

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Plaintext M2M secret — returned once, never stored by Zahy.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; set; } = [];

    public Guid? OwnerIdentityUserId { get; set; }

    /// <summary>One-time set-password token for the owner; never stored by Zahy.</summary>
    public string? OwnerSetPasswordToken { get; set; }
}
