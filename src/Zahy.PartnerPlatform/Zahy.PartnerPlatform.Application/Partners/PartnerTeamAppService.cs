using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Zahy.Identity.Auditing;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;
using Zahy.Identity.Roles;

namespace Zahy.PartnerPlatform.Partners;

[Authorize(ZahyPermissions.Roles.Manage)]
public class PartnerTeamAppService : ApplicationService, IPartnerTeamAppService
{
    private readonly IRepository<PartnerUser, Guid> _partnerUserRepository;
    private readonly PartnerAccessGuard _accessGuard;
    private readonly IPartnerUserInviteService _partnerUserInviteService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IAdminAuditLogger _auditLogger;

    public PartnerTeamAppService(
        IRepository<PartnerUser, Guid> partnerUserRepository,
        PartnerAccessGuard accessGuard,
        IPartnerUserInviteService partnerUserInviteService,
        IGuidGenerator guidGenerator,
        IAdminAuditLogger auditLogger)
    {
        _partnerUserRepository = partnerUserRepository;
        _accessGuard = accessGuard;
        _partnerUserInviteService = partnerUserInviteService;
        _guidGenerator = guidGenerator;
        _auditLogger = auditLogger;
    }

    public virtual async Task<IReadOnlyList<PartnerTeamMemberDto>> GetMembersAsync()
    {
        _ = await _accessGuard.GetRequiredPartnerIdAsync();

        var members = await _partnerUserRepository.GetListAsync();
        return members.Select(ToDto).ToList();
    }

    public virtual async Task<PartnerTeamMemberDto> GetMemberAsync(Guid partnerUserId)
    {
        _ = await _accessGuard.GetRequiredPartnerIdAsync();

        var member = await _partnerUserRepository.FindAsync(partnerUserId);
        if (member == null)
        {
            throw new AbpAuthorizationException("Partner team member access denied.");
        }

        return ToDto(member);
    }

    [UnitOfWork]
    public virtual async Task<PartnerTeamInviteResultDto> InviteAsync(PartnerTeamInviteInput input)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();

        if (!PartnerTeamRoles.IsInvitable(input.Role))
        {
            throw new BusinessException(PartnerPlatformErrorCodes.InvalidPartnerTeamRole)
                .WithData("Role", input.Role);
        }

        var inviteResult = await _partnerUserInviteService.InviteAsync(new PartnerUserInviteRequest
        {
            PartnerId = partnerId,
            Email = input.Email,
            Role = input.Role
        });

        var existing = await _partnerUserRepository.FirstOrDefaultAsync(x =>
            x.PartnerId == partnerId && x.IdentityUserId == inviteResult.IdentityUserId);

        PartnerUser partnerUser;
        if (existing != null)
        {
            if (existing.Status != PartnerUserStatus.Invited)
            {
                throw new BusinessException(PartnerPlatformErrorCodes.PartnerUserAlreadyInvited)
                    .WithData("Email", input.Email);
            }

            partnerUser = existing;
        }
        else
        {
            partnerUser = PartnerUser.CreateInvited(
                _guidGenerator.Create(),
                partnerId,
                inviteResult.IdentityUserId,
                input.Role,
                Clock.Now);

            await _partnerUserRepository.InsertAsync(partnerUser, autoSave: true);
        }

        await _auditLogger.LogAsync(
            "Partner.Team.Invite",
            targetType: "PartnerUser",
            targetId: partnerUser.Id.ToString(),
            result: AdminAuditResults.Success,
            extraData: input.Email);

        return new PartnerTeamInviteResultDto
        {
            PartnerUserId = partnerUser.Id,
            IdentityUserId = inviteResult.IdentityUserId,
            Email = inviteResult.Email,
            Role = input.Role,
            SetPasswordToken = inviteResult.SetPasswordToken
        };
    }

    private static PartnerTeamMemberDto ToDto(PartnerUser member) =>
        new()
        {
            PartnerUserId = member.Id,
            IdentityUserId = member.IdentityUserId,
            Role = member.Role,
            Status = member.Status,
            InvitedAt = member.InvitedAt
        };
}
