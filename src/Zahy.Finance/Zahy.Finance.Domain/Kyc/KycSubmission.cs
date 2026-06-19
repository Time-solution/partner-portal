using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

/// <summary>Append-only self-reported KYC data — sensitive fields stored protected at rest.</summary>
public class KycSubmission : AggregateRoot<Guid>
{
    public KycEntityKind EntityKind { get; private set; }

    public Guid EntityId { get; private set; }

    public int Version { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public string ProtectedLegalNameAr { get; private set; } = string.Empty;

    public string ProtectedLegalNameEn { get; private set; } = string.Empty;

    public string ProtectedCommercialRegistrationNumber { get; private set; } = string.Empty;

    public string ProtectedVatNumber { get; private set; } = string.Empty;

    public string ProtectedIban { get; private set; } = string.Empty;

    public string ProtectedLegalAddress { get; private set; } = string.Empty;

    protected KycSubmission()
    {
    }

    public static KycSubmission Create(
        Guid id,
        KycEntityKind entityKind,
        Guid entityId,
        int version,
        Guid? submittedByUserId,
        DateTime submittedAt,
        ProtectedKycFieldBundle protectedFields)
    {
        Check.NotNull(protectedFields, nameof(protectedFields));

        if (entityId == Guid.Empty)
        {
            throw new BusinessException(FinanceErrorCodes.KycSubmissionNotFound)
                .WithData("Field", nameof(entityId));
        }

        return new KycSubmission
        {
            Id = id,
            EntityKind = entityKind,
            EntityId = entityId,
            Version = version,
            SubmittedByUserId = submittedByUserId,
            SubmittedAt = submittedAt,
            ProtectedLegalNameAr = protectedFields.LegalNameAr,
            ProtectedLegalNameEn = protectedFields.LegalNameEn,
            ProtectedCommercialRegistrationNumber = protectedFields.CommercialRegistrationNumber,
            ProtectedVatNumber = protectedFields.VatNumber,
            ProtectedIban = protectedFields.Iban,
            ProtectedLegalAddress = protectedFields.LegalAddress
        };
    }
}

public sealed class ProtectedKycFieldBundle
{
    public string LegalNameAr { get; init; } = string.Empty;

    public string LegalNameEn { get; init; } = string.Empty;

    public string CommercialRegistrationNumber { get; init; } = string.Empty;

    public string VatNumber { get; init; } = string.Empty;

    public string Iban { get; init; } = string.Empty;

    public string LegalAddress { get; init; } = string.Empty;
}
