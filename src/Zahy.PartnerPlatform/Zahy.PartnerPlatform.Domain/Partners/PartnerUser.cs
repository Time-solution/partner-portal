using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.PartnerPlatform.Partners;

/// <summary>Links an ABP identity user to a partner aggregate with a partner-scope role.</summary>
public class PartnerUser : FullAuditedEntity<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid IdentityUserId { get; private set; }

    public string Role { get; private set; } = string.Empty;

    public PartnerUserStatus Status { get; private set; }

    public DateTime? InvitedAt { get; private set; }

    public DateTime? ActivatedAt { get; private set; }

    protected PartnerUser()
    {
    }

    public PartnerUser(
        Guid id,
        Guid partnerId,
        Guid identityUserId,
        string role,
        PartnerUserStatus status,
        DateTime? invitedAt = null,
        DateTime? activatedAt = null)
        : base(id)
    {
        PartnerId = partnerId;
        IdentityUserId = identityUserId;
        Role = Check.NotNullOrWhiteSpace(role, nameof(role));
        Status = status;
        InvitedAt = invitedAt;
        ActivatedAt = activatedAt;
    }

    public static PartnerUser CreateInvited(Guid id, Guid partnerId, Guid identityUserId, string role, DateTime invitedAt) =>
        new(id, partnerId, identityUserId, role, PartnerUserStatus.Invited, invitedAt, activatedAt: null);
}
