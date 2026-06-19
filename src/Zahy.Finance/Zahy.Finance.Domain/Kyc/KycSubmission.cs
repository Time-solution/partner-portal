using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

/// <summary>Minimal KYC submission stub — expanded in Step 3.</summary>
public class KycSubmission : AggregateRoot<Guid>
{
    public KycEntityKind EntityKind { get; private set; }

    public Guid EntityId { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    protected KycSubmission()
    {
    }

    public KycSubmission(Guid id, KycEntityKind entityKind, Guid entityId, DateTime submittedAt)
    {
        Id = id;
        EntityKind = entityKind;
        EntityId = entityId;
        SubmittedAt = submittedAt;
    }
}
