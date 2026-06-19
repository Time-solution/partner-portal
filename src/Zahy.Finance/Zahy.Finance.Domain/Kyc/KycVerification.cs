using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

/// <summary>Platform KYC verification — full state machine in Step 3.</summary>
public class KycVerification : AggregateRoot<Guid>
{
    public KycEntityKind EntityKind { get; private set; }

    public Guid EntityId { get; private set; }

    public Guid KycSubmissionId { get; private set; }

    public KycVerificationStatus Status { get; private set; }

    public DateTime? VerifiedAt { get; private set; }

    protected KycVerification()
    {
    }

    public static KycVerification CreateVerified(
        Guid id,
        KycEntityKind entityKind,
        Guid entityId,
        Guid kycSubmissionId,
        DateTime verifiedAt) =>
        new()
        {
            Id = id,
            EntityKind = entityKind,
            EntityId = entityId,
            KycSubmissionId = kycSubmissionId,
            Status = KycVerificationStatus.Verified,
            VerifiedAt = verifiedAt
        };

    public bool IsVerified => Status == KycVerificationStatus.Verified;
}
