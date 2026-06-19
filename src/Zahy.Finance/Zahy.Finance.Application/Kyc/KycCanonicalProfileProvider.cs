using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Zahy.Finance;

public class KycCanonicalProfileProvider : ApplicationService, IKycCanonicalProfileProvider
{
    private readonly IRepository<KycVerification, Guid> _verificationRepository;

    public KycCanonicalProfileProvider(IRepository<KycVerification, Guid> verificationRepository)
    {
        _verificationRepository = verificationRepository;
    }

    public async Task<KycCanonicalProfileDto?> GetLatestVerifiedProfileAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _verificationRepository.GetQueryableAsync();
        var verification = queryable
            .Where(x =>
                x.EntityKind == entityKind &&
                x.EntityId == entityId &&
                x.Status == KycVerificationStatus.Verified)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefault();

        if (verification == null)
        {
            return null;
        }

        return MapProfile(verification);
    }

    internal static KycCanonicalProfileDto MapProfile(KycVerification verification) =>
        new()
        {
            VerificationId = verification.Id,
            LegalNameAr = verification.VerifiedLegalNameAr ?? string.Empty,
            LegalNameEn = verification.VerifiedLegalNameEn ?? string.Empty,
            CommercialRegistrationNumber = verification.VerifiedCommercialRegistrationNumber ?? string.Empty,
            VatNumber = verification.VerifiedVatNumber ?? string.Empty,
            Iban = verification.VerifiedIban ?? string.Empty,
            LegalAddress = verification.VerifiedLegalAddress ?? string.Empty
        };
}

public class FinanceDocumentKycBlockBuilder : ApplicationService, IFinanceDocumentKycBlockBuilder
{
    private readonly IKycCanonicalProfileProvider _canonicalProfileProvider;

    public FinanceDocumentKycBlockBuilder(IKycCanonicalProfileProvider canonicalProfileProvider)
    {
        _canonicalProfileProvider = canonicalProfileProvider;
    }

    public async Task<FinanceDocumentKycBlockDto> BuildAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _canonicalProfileProvider.GetLatestVerifiedProfileAsync(
            entityKind,
            entityId,
            cancellationToken);

        if (profile == null)
        {
            throw new BusinessException(FinanceErrorCodes.KycNotVerified)
                .WithData("EntityKind", entityKind.ToString())
                .WithData("EntityId", entityId);
        }

        return new FinanceDocumentKycBlockDto
        {
            LegalNameAr = profile.LegalNameAr,
            LegalNameEn = profile.LegalNameEn,
            CommercialRegistrationNumber = profile.CommercialRegistrationNumber,
            VatNumber = profile.VatNumber,
            Iban = profile.Iban,
            LegalAddress = profile.LegalAddress
        };
    }
}
