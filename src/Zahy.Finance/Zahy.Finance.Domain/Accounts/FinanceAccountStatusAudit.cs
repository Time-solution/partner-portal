using System;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class FinanceAccountStatusAudit : Entity<Guid>
{
    public FinanceAccountKind AccountKind { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid EntityId { get; private set; }

    public FinanceAccountStatus FromStatus { get; private set; }

    public FinanceAccountStatus ToStatus { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public DateTime TransitionedAt { get; private set; }

    public string? Reason { get; private set; }

    protected FinanceAccountStatusAudit()
    {
    }

    public static FinanceAccountStatusAudit Record(
        Guid id,
        FinanceAccountKind accountKind,
        Guid accountId,
        Guid entityId,
        FinanceAccountStatus fromStatus,
        FinanceAccountStatus toStatus,
        DateTime transitionedAt,
        Guid? actorUserId = null,
        string? reason = null) =>
        new()
        {
            Id = id,
            AccountKind = accountKind,
            AccountId = accountId,
            EntityId = entityId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            TransitionedAt = transitionedAt,
            Reason = reason?.Trim()
        };
}
