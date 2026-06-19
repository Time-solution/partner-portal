using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

[Authorize(ZahyPermissions.Finance.KycReview)]
[DisableAuditing]
public class KycVerificationService : ApplicationService, IKycVerificationService
{
    private readonly IRepository<KycVerification, Guid> _verificationRepository;

    public KycVerificationService(IRepository<KycVerification, Guid> verificationRepository)
    {
        _verificationRepository = verificationRepository;
    }

    [UnitOfWork]
    public virtual async Task<KycVerificationDto> StartReviewAsync(
        Guid verificationId,
        CancellationToken cancellationToken = default)
    {
        var verification = await GetVerificationAsync(verificationId, cancellationToken);
        var fromStatus = verification.Status;
        verification.MarkUnderReview(CurrentUser.GetId(), Clock.Now);
        await _verificationRepository.UpdateAsync(verification, autoSave: true, cancellationToken: cancellationToken);

        KycAuditLog.LogVerificationTransition(Logger, verificationId, fromStatus, verification.Status);
        return ToDto(verification);
    }

    [UnitOfWork]
    public virtual async Task<KycVerificationDto> VerifyAsync(
        KycVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        var verification = await GetVerificationAsync(request.VerificationId, cancellationToken);
        var fromStatus = verification.Status;
        verification.MarkVerified(request.CanonicalFields, CurrentUser.GetId(), Clock.Now);
        await _verificationRepository.UpdateAsync(verification, autoSave: true, cancellationToken: cancellationToken);

        KycAuditLog.LogVerificationTransition(Logger, verification.Id, fromStatus, verification.Status);
        return ToDto(verification);
    }

    [UnitOfWork]
    public virtual async Task<KycVerificationDto> RejectAsync(
        KycRejectRequest request,
        CancellationToken cancellationToken = default)
    {
        var verification = await GetVerificationAsync(request.VerificationId, cancellationToken);
        var fromStatus = verification.Status;
        verification.MarkRejected(CurrentUser.GetId(), Clock.Now, request.ReviewNotes);
        await _verificationRepository.UpdateAsync(verification, autoSave: true, cancellationToken: cancellationToken);

        KycAuditLog.LogVerificationTransition(Logger, verification.Id, fromStatus, verification.Status);
        return ToDto(verification);
    }

    private async Task<KycVerification> GetVerificationAsync(Guid verificationId, CancellationToken cancellationToken)
    {
        var verification = await _verificationRepository.FindAsync(verificationId, cancellationToken: cancellationToken);
        if (verification == null)
        {
            throw new BusinessException(FinanceErrorCodes.KycVerificationNotFound)
                .WithData("VerificationId", verificationId);
        }

        return verification;
    }

    private static KycVerificationDto ToDto(KycVerification verification) =>
        new()
        {
            VerificationId = verification.Id,
            SubmissionId = verification.KycSubmissionId,
            Status = verification.Status
        };
}
