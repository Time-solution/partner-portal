using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Finance;

[DisableAuditing]
public class KycSubmissionService : ApplicationService, IKycSubmissionService
{
    private readonly IRepository<KycSubmission, Guid> _submissionRepository;
    private readonly IRepository<KycVerification, Guid> _verificationRepository;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly IGuidGenerator _guidGenerator;

    public KycSubmissionService(
        IRepository<KycSubmission, Guid> submissionRepository,
        IRepository<KycVerification, Guid> verificationRepository,
        IKycFieldProtector fieldProtector,
        IGuidGenerator guidGenerator)
    {
        _submissionRepository = submissionRepository;
        _verificationRepository = verificationRepository;
        _fieldProtector = fieldProtector;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task<KycSubmissionResult> SubmitAsync(
        KycSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var version = await GetNextVersionAsync(request.EntityKind, request.EntityId, cancellationToken);
        var submissionId = _guidGenerator.Create();
        var verificationId = _guidGenerator.Create();
        var submittedAt = Clock.Now;

        var protectedFields = ProtectPayload(request.Payload);
        var submission = KycSubmission.Create(
            submissionId,
            request.EntityKind,
            request.EntityId,
            version,
            request.SubmittedByUserId,
            submittedAt,
            protectedFields);

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            request.EntityKind,
            request.EntityId,
            submissionId);

        await _submissionRepository.InsertAsync(submission, autoSave: true, cancellationToken: cancellationToken);
        await _verificationRepository.InsertAsync(verification, autoSave: true, cancellationToken: cancellationToken);

        KycAuditLog.LogSubmissionCreated(
            Logger,
            submissionId,
            request.EntityKind,
            request.EntityId,
            version);

        return new KycSubmissionResult
        {
            SubmissionId = submissionId,
            VerificationId = verificationId,
            Version = version,
            Status = verification.Status
        };
    }

    private ProtectedKycFieldBundle ProtectPayload(KycSubmissionPayload payload) =>
        new()
        {
            LegalNameAr = _fieldProtector.Protect(payload.LegalNameAr),
            LegalNameEn = _fieldProtector.Protect(payload.LegalNameEn),
            CommercialRegistrationNumber = _fieldProtector.Protect(payload.CommercialRegistrationNumber),
            VatNumber = _fieldProtector.Protect(payload.VatNumber),
            Iban = _fieldProtector.Protect(payload.Iban),
            LegalAddress = _fieldProtector.Protect(payload.LegalAddress)
        };

    private async Task<int> GetNextVersionAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var queryable = await _submissionRepository.GetQueryableAsync();
        var latestVersion = queryable
            .Where(x => x.EntityKind == entityKind && x.EntityId == entityId)
            .OrderByDescending(x => x.Version)
            .Select(x => x.Version)
            .FirstOrDefault();

        return latestVersion + 1;
    }
}
