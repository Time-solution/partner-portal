using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.PartnerCatalog.Packages;

namespace Zahy.PartnerCatalog.HttpApi.Packages;

/// <summary>
/// U4 — merchant package browse + selection API. Browse a partner's published packages (merchant SELL/fee
/// view) and select one for instant activation. SELECTION/LINK ONLY — no billing computed, nothing posted.
/// </summary>
[Route("api/partner-catalog/usage-package-selections")]
public class UsagePackageSelectionController : AbpControllerBase
{
    private readonly IUsagePackageSelectionAppService _appService;

    public UsagePackageSelectionController(IUsagePackageSelectionAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("published/{partnerId:guid}")]
    public Task<List<UsagePackageDto>> GetPublishedAsync(Guid partnerId) =>
        _appService.GetPublishedAsync(partnerId);

    [HttpPost]
    public Task<UsagePackageSelectionDto> SelectAsync([FromBody] SelectUsagePackageInput input) =>
        _appService.SelectAsync(input);

    [HttpPost("{id:guid}/end")]
    public Task<UsagePackageSelectionDto> EndAsync(Guid id) =>
        _appService.EndAsync(id);

    [HttpGet("merchant/{tenantId:guid}")]
    public Task<List<UsagePackageSelectionDto>> GetForMerchantAsync(Guid tenantId) =>
        _appService.GetForMerchantAsync(tenantId);
}
