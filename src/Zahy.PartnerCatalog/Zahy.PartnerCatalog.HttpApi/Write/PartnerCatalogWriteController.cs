using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerCatalog.Write;

/// <summary>
/// Partner/admin catalog authoring API — create, update, publish, archive.
/// Snapshot-on-activate stays OFF (Phase 3).
/// </summary>
[Route("api/partner-catalog/write")]
public class PartnerCatalogWriteController : AbpControllerBase
{
    private readonly IPartnerCatalogWriteAppService _writeAppService;

    public PartnerCatalogWriteController(IPartnerCatalogWriteAppService writeAppService)
    {
        _writeAppService = writeAppService;
    }

    [HttpPost("items")]
    public Task<Read.PartnerCatalogItemReadDto> CreateAsync([FromBody] CreatePartnerCatalogItemInput input) =>
        _writeAppService.CreateAsync(input);

    [HttpPut("items/{id:guid}")]
    public Task<Read.PartnerCatalogItemReadDto> UpdateAsync(Guid id, [FromBody] UpdatePartnerCatalogItemInput input) =>
        _writeAppService.UpdateAsync(id, input);

    [HttpPost("items/{id:guid}/publish")]
    public Task<Read.PartnerCatalogItemReadDto> PublishAsync(Guid id) =>
        _writeAppService.PublishAsync(id);

    [HttpPost("items/{id:guid}/archive")]
    public Task<Read.PartnerCatalogItemReadDto> ArchiveAsync(Guid id) =>
        _writeAppService.ArchiveAsync(id);
}
