using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.PartnerCatalog.Packages;

namespace Zahy.PartnerCatalog.HttpApi.Packages;

/// <summary>
/// U2 — usage package authoring API (partner self-publish + admin manages-all). CONFIG ONLY: no billing
/// is computed and no journal posted (that is the gated U3 phase).
/// </summary>
[Route("api/partner-catalog/usage-packages")]
public class UsagePackageWriteController : AbpControllerBase
{
    private readonly IUsagePackageWriteAppService _appService;

    public UsagePackageWriteController(IUsagePackageWriteAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<List<UsagePackageDto>> GetListAsync([FromQuery] UsagePackagesQuery query) =>
        _appService.GetListAsync(query);

    [HttpPost]
    public Task<UsagePackageDto> CreateAsync([FromBody] CreateUsagePackageInput input) =>
        _appService.CreateAsync(input);

    [HttpPut("{id:guid}")]
    public Task<UsagePackageDto> UpdateAsync(Guid id, [FromBody] UpdateUsagePackageInput input) =>
        _appService.UpdateAsync(id, input);

    [HttpPost("{id:guid}/publish")]
    public Task<UsagePackageDto> PublishAsync(Guid id) =>
        _appService.PublishAsync(id);

    [HttpPost("{id:guid}/archive")]
    public Task<UsagePackageDto> ArchiveAsync(Guid id) =>
        _appService.ArchiveAsync(id);
}
