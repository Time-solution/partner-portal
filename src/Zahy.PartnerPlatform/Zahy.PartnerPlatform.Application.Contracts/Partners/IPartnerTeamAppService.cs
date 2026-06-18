using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerPlatform.Partners;

public interface IPartnerTeamAppService : IApplicationService
{
    Task<IReadOnlyList<PartnerTeamMemberDto>> GetMembersAsync();

    Task<PartnerTeamInviteResultDto> InviteAsync(PartnerTeamInviteInput input);

    Task<PartnerTeamMemberDto> GetMemberAsync(Guid partnerUserId);
}
