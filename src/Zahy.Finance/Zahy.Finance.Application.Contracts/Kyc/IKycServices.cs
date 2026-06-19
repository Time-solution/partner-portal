using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

public interface IKycSubmissionService
{
    Task<KycSubmissionResult> SubmitAsync(
        KycSubmissionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IKycVerificationService
{
    Task<KycVerificationDto> StartReviewAsync(
        Guid verificationId,
        CancellationToken cancellationToken = default);

    Task<KycVerificationDto> VerifyAsync(
        KycVerifyRequest request,
        CancellationToken cancellationToken = default);

    Task<KycVerificationDto> RejectAsync(
        KycRejectRequest request,
        CancellationToken cancellationToken = default);
}

public interface IKycCanonicalProfileProvider
{
    Task<KycCanonicalProfileDto?> GetLatestVerifiedProfileAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken = default);
}

public interface IFinanceDocumentKycBlockBuilder
{
    Task<FinanceDocumentKycBlockDto> BuildAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken = default);
}

public sealed class KycSubmissionRequest
{
    public KycEntityKind EntityKind { get; init; }

    public Guid EntityId { get; init; }

    public Guid? SubmittedByUserId { get; init; }

    public KycSubmissionPayload Payload { get; init; } = new();
}

public sealed class KycSubmissionResult
{
    public Guid SubmissionId { get; init; }

    public Guid VerificationId { get; init; }

    public int Version { get; init; }

    public KycVerificationStatus Status { get; init; }
}

public sealed class KycVerifyRequest
{
    public Guid VerificationId { get; init; }

    public KycVerifiedCanonicalFields CanonicalFields { get; init; } = new();
}

public sealed class KycRejectRequest
{
    public Guid VerificationId { get; init; }

    public string? ReviewNotes { get; init; }
}

public sealed class KycVerificationDto
{
    public Guid VerificationId { get; init; }

    public Guid SubmissionId { get; init; }

    public KycVerificationStatus Status { get; init; }
}

public sealed class KycCanonicalProfileDto
{
    public Guid VerificationId { get; init; }

    public string LegalNameAr { get; init; } = string.Empty;

    public string LegalNameEn { get; init; } = string.Empty;

    public string CommercialRegistrationNumber { get; init; } = string.Empty;

    public string VatNumber { get; init; } = string.Empty;

    public string Iban { get; init; } = string.Empty;

    public string LegalAddress { get; init; } = string.Empty;
}

public sealed class FinanceDocumentKycBlockDto
{
    public string LegalNameAr { get; init; } = string.Empty;

    public string LegalNameEn { get; init; } = string.Empty;

    public string CommercialRegistrationNumber { get; init; } = string.Empty;

    public string VatNumber { get; init; } = string.Empty;

    public string Iban { get; init; } = string.Empty;

    public string LegalAddress { get; init; } = string.Empty;
}
