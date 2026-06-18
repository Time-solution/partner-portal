using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerPlatform.Partners;

[Route("api/admin/partners")]
public class PartnerAdminController : AbpControllerBase
{
    private readonly IPartnerAdminAppService _partnerAdminAppService;

    public PartnerAdminController(IPartnerAdminAppService partnerAdminAppService)
    {
        _partnerAdminAppService = partnerAdminAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<PartnerListItemDto>> GetListAsync([FromQuery] GetPartnersInput input) =>
        _partnerAdminAppService.GetListAsync(input);

    [HttpGet("{id:guid}")]
    public Task<PartnerDto> GetAsync(Guid id) =>
        _partnerAdminAppService.GetAsync(id);

    [HttpPost("{id:guid}/approve")]
    public Task<PartnerApproveResultDto> ApproveAsync(Guid id, [FromBody] PartnerLifecycleActionInput? input = null) =>
        _partnerAdminAppService.ApproveAsync(id, input);

    [HttpPost("{id:guid}/reject")]
    public Task<PartnerDto> RejectAsync(Guid id, [FromBody] PartnerLifecycleActionInput? input = null) =>
        _partnerAdminAppService.RejectAsync(id, input);

    [HttpPost("{id:guid}/suspend")]
    public Task<PartnerDto> SuspendAsync(Guid id, [FromBody] PartnerLifecycleActionInput? input = null) =>
        _partnerAdminAppService.SuspendAsync(id, input);

    [HttpPost("{id:guid}/reactivate")]
    public Task<PartnerDto> ReactivateAsync(Guid id, [FromBody] PartnerLifecycleActionInput? input = null) =>
        _partnerAdminAppService.ReactivateAsync(id, input);

    [HttpPost("{id:guid}/close")]
    public Task<PartnerDto> CloseAsync(Guid id, [FromBody] PartnerLifecycleActionInput? input = null) =>
        _partnerAdminAppService.CloseAsync(id, input);

    [HttpPost("{id:guid}/rotate-m2m-secret")]
    public Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync(Guid id) =>
        _partnerAdminAppService.RotateM2MClientSecretAsync(id);
}
