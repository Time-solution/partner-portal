using System;
using System.ComponentModel.DataAnnotations;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerTeamInviteInput
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class PartnerTeamInviteResultDto
{
    public Guid PartnerUserId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    /// <summary>One-time set-password token; never stored by Zahy.</summary>
    public string SetPasswordToken { get; set; } = string.Empty;
}

public class PartnerTeamMemberDto
{
    public Guid PartnerUserId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string Role { get; set; } = string.Empty;

    public PartnerUserStatus Status { get; set; }

    public DateTime? InvitedAt { get; set; }
}

public class PartnerM2MRotateResultDto
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
