using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Finance;

[Route("api/merchant/account")]
public class MerchantFinancePortalController : AbpControllerBase
{
    private readonly IFinanceMerchantPortalAppService _portalAppService;

    public MerchantFinancePortalController(IFinanceMerchantPortalAppService portalAppService)
    {
        _portalAppService = portalAppService;
    }

    [HttpGet]
    public Task<FinancePortalAccountDto> GetAccountAsync() =>
        _portalAppService.GetAccountAsync();

    [HttpGet("postings")]
    public Task<PagedResultDto<FinancePortalPostingRowDto>> GetPostingsAsync(
        [FromQuery] FinancePortalPostingsRequest request) =>
        _portalAppService.GetPostingsAsync(request);

    [HttpGet("documents")]
    public Task<System.Collections.Generic.IReadOnlyList<FinancePortalDocumentListItemDto>> GetDocumentsAsync() =>
        _portalAppService.GetDocumentsAsync();

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] FinancePortalExportRequest request)
    {
        var download = await _portalAppService.ExportPostingsAsync(request);
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpGet("documents/{id:guid}/download")]
    public async Task<IActionResult> DownloadDocumentAsync(Guid id)
    {
        var download = await _portalAppService.DownloadDocumentAsync(id);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
