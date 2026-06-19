using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Volo.Abp.Data;
using Zahy.Identity;
using Zahy.Identity.Roles;
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerPlatform.Partners;

[Authorize(ZahyPermissions.Partners.Manage)]
public class PartnerAdminAppService : ApplicationService, IPartnerAdminAppService
{
    private readonly IRepository<Partner, Guid> _partnerRepository;
    private readonly PartnerLifecycleManager _lifecycleManager;
    private readonly IAdminAuditLogger _auditLogger;
    private readonly IPartnerM2MClientProvisioner _m2mClientProvisioner;
    private readonly IPartnerUserInviteService _partnerUserInviteService;
    private readonly IRepository<PartnerUser, Guid> _partnerUserRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IDataFilter _dataFilter;

    public PartnerAdminAppService(
        IRepository<Partner, Guid> partnerRepository,
        PartnerLifecycleManager lifecycleManager,
        IAdminAuditLogger auditLogger,
        IPartnerM2MClientProvisioner m2mClientProvisioner,
        IPartnerUserInviteService partnerUserInviteService,
        IRepository<PartnerUser, Guid> partnerUserRepository,
        IGuidGenerator guidGenerator,
        IDataFilter dataFilter)
    {
        _partnerRepository = partnerRepository;
        _lifecycleManager = lifecycleManager;
        _auditLogger = auditLogger;
        _m2mClientProvisioner = m2mClientProvisioner;
        _partnerUserInviteService = partnerUserInviteService;
        _partnerUserRepository = partnerUserRepository;
        _guidGenerator = guidGenerator;
        _dataFilter = dataFilter;
    }

    public virtual async Task<PagedResultDto<PartnerListItemDto>> GetListAsync(GetPartnersInput input)
    {
        var queryable = await _partnerRepository.GetQueryableAsync();

        queryable = queryable
            .WhereIf(input.Status.HasValue, p => p.Status == input.Status)
            .WhereIf(input.Type.HasValue, p => p.Type == input.Type)
            .WhereIf(
                !input.Filter.IsNullOrWhiteSpace(),
                p => p.LegalName.Contains(input.Filter!) ||
                     (p.TradeName != null && p.TradeName.Contains(input.Filter!)) ||
                     p.PrimaryContactEmail.Contains(input.Filter!));

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        queryable = ApplySorting(queryable, input);
        queryable = queryable.PageBy(input);

        var items = await AsyncExecuter.ToListAsync(queryable);

        return new PagedResultDto<PartnerListItemDto>(
            totalCount,
            items.Select(PartnerDtoMapper.ToListItem).ToList());
    }

    public virtual async Task<PartnerDto> GetAsync(Guid id)
    {
        var partner = await GetPartnerOrThrowAsync(id);
        return PartnerDtoMapper.ToDto(partner);
    }

    [UnitOfWork]
    public virtual async Task<PartnerApproveResultDto> ApproveAsync(Guid id, PartnerLifecycleActionInput? input = null)
    {
        var partner = await GetPartnerOrThrowAsync(id);

        if (!string.IsNullOrWhiteSpace(partner.OpenIddictClientId))
        {
            throw new BusinessException(PartnerPlatformErrorCodes.PartnerM2MClientAlreadyProvisioned)
                .WithData("PartnerId", partner.Id)
                .WithData("ClientId", partner.OpenIddictClientId);
        }

        try
        {
            _lifecycleManager.EnsureCanApprove(partner);

            var scopes = PartnerTypeScopePolicy.GetDefaultScopes(partner.Type);
            var provisionResult = await _m2mClientProvisioner.ProvisionAsync(new PartnerM2MClientProvisionRequest
            {
                PartnerId = partner.Id,
                DisplayName = partner.LegalName,
                Scopes = scopes
            });

            _lifecycleManager.Approve(partner);
            partner.AssignOpenIddictClient(provisionResult.ClientId);

            var ownerInvite = await _partnerUserInviteService.InviteAsync(new PartnerUserInviteRequest
            {
                PartnerId = partner.Id,
                Email = partner.PrimaryContactEmail,
                Role = ZahyRoles.PartnerOwner,
                BypassRoleAssignmentPolicy = true
            });

            await _partnerUserRepository.InsertAsync(
                PartnerUser.CreateInvited(
                    _guidGenerator.Create(),
                    partner.Id,
                    ownerInvite.IdentityUserId,
                    ZahyRoles.PartnerOwner,
                    Clock.Now),
                autoSave: true);

            using (_dataFilter.Disable<IPartnerDataFilter>())
            {
                await _partnerRepository.UpdateAsync(partner, autoSave: true);
            }

            await _auditLogger.LogAsync(
                "Partner.Approve",
                targetType: "Partner",
                targetId: partner.Id.ToString(),
                result: AdminAuditResults.Success,
                extraData: $"clientId={provisionResult.ClientId}; owner={ownerInvite.IdentityUserId}");

            return new PartnerApproveResultDto
            {
                Partner = PartnerDtoMapper.ToDto(partner),
                ClientId = provisionResult.ClientId,
                ClientSecret = provisionResult.ClientSecret,
                Scopes = scopes,
                OwnerIdentityUserId = ownerInvite.IdentityUserId,
                OwnerSetPasswordToken = ownerInvite.SetPasswordToken
            };
        }
        catch (BusinessException ex) when (
            ex.Code == PartnerPlatformErrorCodes.IllegalLifecycleTransition ||
            ex.Code == PartnerPlatformErrorCodes.BankInfoRequiredForApproval ||
            ex.Code == ZahyIdentityErrorCodes.PartnerM2MClientAlreadyExists)
        {
            await _auditLogger.LogAsync(
                "Partner.Approve",
                targetType: "Partner",
                targetId: partner.Id.ToString(),
                result: AdminAuditResults.Denied,
                extraData: ex.Code);

            throw;
        }
    }

    [UnitOfWork]
    public virtual async Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync(Guid id)
    {
        var partner = await GetPartnerOrThrowAsync(id);

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

        var rotateResult = await _m2mClientProvisioner.RotateSecretAsync(partner.OpenIddictClientId);

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

    [UnitOfWork]
    public virtual async Task<PartnerDto> RejectAsync(Guid id, PartnerLifecycleActionInput? input = null)
    {
        return await ExecuteLifecycleAsync(id, "Partner.Reject", partner =>
        {
            _lifecycleManager.Reject(partner, input?.Notes);
            return Task.CompletedTask;
        }, input?.Notes);
    }

    [UnitOfWork]
    public virtual async Task<PartnerDto> SuspendAsync(Guid id, PartnerLifecycleActionInput? input = null)
    {
        return await ExecuteLifecycleAsync(id, "Partner.Suspend", partner =>
        {
            _lifecycleManager.Suspend(partner, input?.Notes);
            return Task.CompletedTask;
        }, input?.Notes);
    }

    [UnitOfWork]
    public virtual async Task<PartnerDto> ReactivateAsync(Guid id, PartnerLifecycleActionInput? input = null)
    {
        return await ExecuteLifecycleAsync(id, "Partner.Reactivate", partner =>
        {
            _lifecycleManager.Reactivate(partner, input?.Notes);
            return Task.CompletedTask;
        }, input?.Notes);
    }

    [UnitOfWork]
    public virtual async Task<PartnerDto> CloseAsync(Guid id, PartnerLifecycleActionInput? input = null)
    {
        return await ExecuteLifecycleAsync(id, "Partner.Close", partner =>
        {
            _lifecycleManager.Close(partner, input?.Notes);
            return Task.CompletedTask;
        }, input?.Notes);
    }

    private async Task<PartnerDto> ExecuteLifecycleAsync(
        Guid id,
        string auditAction,
        Func<Partner, Task> transition,
        string? notes)
    {
        var partner = await GetPartnerOrThrowAsync(id);

        try
        {
            await transition(partner);
            await _partnerRepository.UpdateAsync(partner, autoSave: true);
            await _auditLogger.LogAsync(
                auditAction,
                targetType: "Partner",
                targetId: partner.Id.ToString(),
                result: AdminAuditResults.Success,
                extraData: notes);

            return PartnerDtoMapper.ToDto(partner);
        }
        catch (BusinessException ex) when (
            ex.Code == PartnerPlatformErrorCodes.IllegalLifecycleTransition ||
            ex.Code == PartnerPlatformErrorCodes.BankInfoRequiredForApproval)
        {
            await _auditLogger.LogAsync(
                auditAction,
                targetType: "Partner",
                targetId: partner.Id.ToString(),
                result: AdminAuditResults.Denied,
                extraData: ex.Code);

            throw;
        }
    }

    private async Task<Partner> GetPartnerOrThrowAsync(Guid id)
    {
        var partner = await _partnerRepository.FindAsync(id);
        if (partner == null)
        {
            throw new BusinessException(PartnerPlatformErrorCodes.PartnerNotFound)
                .WithData("PartnerId", id);
        }

        return partner;
    }

    private static IQueryable<Partner> ApplySorting(IQueryable<Partner> queryable, GetPartnersInput input)
    {
        if (!input.Sorting.IsNullOrWhiteSpace())
        {
            return queryable; // Default EF sort; explicit dynamic sort deferred.
        }

        return queryable.OrderByDescending(p => p.CreationTime);
    }
}
