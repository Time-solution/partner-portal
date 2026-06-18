using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.PartnerPlatform.Partners;

[Authorize]
public class PartnerCredentialsAppService : ApplicationService, IPartnerCredentialsAppService
{
    private readonly IRepository<Partner, Guid> _partnerRepository;
    private readonly PartnerAccessGuard _accessGuard;
    private readonly IPartnerM2MClientProvisioner _m2mClientProvisioner;
    private readonly IAdminAuditLogger _auditLogger;

    public PartnerCredentialsAppService(
        IRepository<Partner, Guid> partnerRepository,
        PartnerAccessGuard accessGuard,
        IPartnerM2MClientProvisioner m2mClientProvisioner,
        IAdminAuditLogger auditLogger)
    {
        _partnerRepository = partnerRepository;
        _accessGuard = accessGuard;
        _m2mClientProvisioner = m2mClientProvisioner;
        _auditLogger = auditLogger;
    }

    [UnitOfWork]
    public virtual async Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync()
    {
        if (!CurrentUser.IsInRole(ZahyRoles.PartnerOwner))
        {
            throw new AbpAuthorizationException("Only the partner owner may rotate M2M credentials.");
        }

        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        var partner = await _partnerRepository.GetAsync(partnerId);
        EnsureActiveWithM2MClient(partner);

        var rotateResult = await _m2mClientProvisioner.RotateSecretAsync(partner.OpenIddictClientId!);

        await _auditLogger.LogAsync(
            "Partner.M2M.Rotate",
            targetType: "Partner",
            targetId: partner.Id.ToString(),
            result: AdminAuditResults.Success,
            extraData: rotateResult.ClientId);

        return new PartnerM2MRotateResultDto
        {
            ClientId = rotateResult.ClientId,
            ClientSecret = rotateResult.ClientSecret
        };
    }

    private static void EnsureActiveWithM2MClient(Partner partner)
    {
        if (partner.Status != PartnerStatus.Active)
        {
            throw new BusinessException(PartnerPlatformErrorCodes.PartnerNotActive)
                .WithData("PartnerId", partner.Id);
        }

        if (string.IsNullOrWhiteSpace(partner.OpenIddictClientId))
        {
            throw new BusinessException(PartnerPlatformErrorCodes.PartnerM2MClientNotProvisioned)
                .WithData("PartnerId", partner.Id);
        }
    }
}
