using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerPlatform.Partners;

[Route("api/partner/team")]
public class PartnerTeamController : AbpControllerBase
{
    private readonly IPartnerTeamAppService _partnerTeamAppService;

    public PartnerTeamController(IPartnerTeamAppService partnerTeamAppService)
    {
        _partnerTeamAppService = partnerTeamAppService;
    }

    [HttpGet]
    public Task<IReadOnlyList<PartnerTeamMemberDto>> GetMembersAsync() =>
        _partnerTeamAppService.GetMembersAsync();

    [HttpGet("{partnerUserId:guid}")]
    public Task<PartnerTeamMemberDto> GetMemberAsync(Guid partnerUserId) =>
        _partnerTeamAppService.GetMemberAsync(partnerUserId);

    [HttpPost("invite")]
    public Task<PartnerTeamInviteResultDto> InviteAsync([FromBody] PartnerTeamInviteInput input) =>
        _partnerTeamAppService.InviteAsync(input);
}
