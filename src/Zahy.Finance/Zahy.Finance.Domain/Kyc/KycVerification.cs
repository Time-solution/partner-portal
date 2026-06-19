using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

/// <summary>Platform KYC verification lifecycle — one row per submission review.</summary>
public class KycVerification : AggregateRoot<Guid>
{
    public KycEntityKind EntityKind { get; private set; }

    public Guid EntityId { get; private set; }

    public Guid KycSubmissionId { get; private set; }

    public KycVerificationStatus Status { get; private set; }

    public Guid? ReviewerUserId { get; private set; }

    public DateTime? ReviewedAt { get; private set; }

    public string? ReviewNotes { get; private set; }

    public DateTime? VerifiedAt { get; private set; }

    public string? VerifiedLegalNameAr { get; private set; }

    public string? VerifiedLegalNameEn { get; private set; }

    public string? VerifiedCommercialRegistrationNumber { get; private set; }

    public string? VerifiedVatNumber { get; private set; }

    public string? VerifiedIban { get; private set; }

    public string? VerifiedLegalAddress { get; private set; }

    protected KycVerification()
    {
    }

    public static KycVerification CreateForSubmission(
        Guid id,
        KycEntityKind entityKind,
        Guid entityId,
        Guid kycSubmissionId) =>
        new()
        {
            Id = id,
            EntityKind = entityKind,
            EntityId = entityId,
            KycSubmissionId = kycSubmissionId,
            Status = KycVerificationStatus.Submitted
        };

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
            VerifiedAt = verifiedAt,
            ReviewedAt = verifiedAt
        };

    public bool IsVerified => Status == KycVerificationStatus.Verified;

    public void MarkUnderReview(Guid reviewerUserId, DateTime reviewedAt)
    {
        EnsureTransition(KycVerificationStatus.UnderReview);
        ReviewerUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        Status = KycVerificationStatus.UnderReview;
    }

    public void MarkVerified(KycVerifiedCanonicalFields canonical, Guid reviewerUserId, DateTime verifiedAt)
    {
        Check.NotNull(canonical, nameof(canonical));
        EnsureTransition(KycVerificationStatus.Verified);

        ReviewerUserId = reviewerUserId;
        ReviewedAt = verifiedAt;
        VerifiedAt = verifiedAt;
        Status = KycVerificationStatus.Verified;
        VerifiedLegalNameAr = canonical.LegalNameAr.Trim();
        VerifiedLegalNameEn = canonical.LegalNameEn.Trim();
        VerifiedCommercialRegistrationNumber = canonical.CommercialRegistrationNumber.Trim();
        VerifiedVatNumber = canonical.VatNumber.Trim();
        VerifiedIban = canonical.Iban.Trim();
        VerifiedLegalAddress = canonical.LegalAddress.Trim();
    }

    public void MarkRejected(Guid reviewerUserId, DateTime reviewedAt, string? reviewNotes)
    {
        EnsureTransition(KycVerificationStatus.Rejected);
        ReviewerUserId = reviewerUserId;
        ReviewedAt = reviewedAt;
        ReviewNotes = reviewNotes?.Trim();
        Status = KycVerificationStatus.Rejected;
    }

    private void EnsureTransition(KycVerificationStatus targetStatus)
    {
        var allowed = (Status, targetStatus) switch
        {
            (KycVerificationStatus.Submitted, KycVerificationStatus.UnderReview) => true,
            (KycVerificationStatus.UnderReview, KycVerificationStatus.Verified) => true,
            (KycVerificationStatus.UnderReview, KycVerificationStatus.Rejected) => true,
            _ => false
        };

        if (!allowed)
        {
            throw new BusinessException(FinanceErrorCodes.IllegalKycTransition)
                .WithData("From", Status.ToString())
                .WithData("To", targetStatus.ToString());
        }
    }
}
