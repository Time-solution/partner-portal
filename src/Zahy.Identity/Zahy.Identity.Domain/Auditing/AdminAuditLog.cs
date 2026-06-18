using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Zahy.Identity.Auditing;

/// <summary>
/// An append-only record of a consequential admin action: who (actor), what
/// (action), on what (target), the outcome, and when (CreationTime from the base).
/// </summary>
public class AdminAuditLog : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid? ActorUserId { get; protected set; }

    public string ActorUserName { get; protected set; } = string.Empty;

    public string Action { get; protected set; } = string.Empty;

    public string? TargetType { get; protected set; }

    public string? TargetId { get; protected set; }

    public string Result { get; protected set; } = string.Empty;

    /// <summary>Partner aggregate context, when the action is partner-scoped.</summary>
    public Guid? PartnerId { get; protected set; }

    public string? ExtraData { get; protected set; }

    protected AdminAuditLog()
    {
    }

    public AdminAuditLog(
        Guid id,
        Guid? actorUserId,
        string actorUserName,
        string action,
        string result,
        Guid? tenantId,
        Guid? partnerId,
        string? targetType = null,
        string? targetId = null,
        string? extraData = null)
        : base(id)
    {
        ActorUserId = actorUserId;
        ActorUserName = actorUserName;
        Action = action;
        Result = result;
        TenantId = tenantId;
        PartnerId = partnerId;
        TargetType = targetType;
        TargetId = targetId;
        ExtraData = extraData;
    }
}
