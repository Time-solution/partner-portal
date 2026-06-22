using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

/// <summary>
/// Platform-side (accountant / admin) invoice endpoints. Authoring a manual invoice requires
/// <see cref="ZahyPermissions.Finance.WriteManualInvoice"/>; reading / downloading requires
/// <see cref="ZahyPermissions.Finance.ReadAll"/>. Partner/merchant principals never reach these routes.
/// </summary>
[Authorize]
[Route("api/finance/invoices")]
public class FinanceInvoiceAdminController : AbpControllerBase
{
    private readonly IFinanceDocumentAppService _financeDocumentAppService;

    public FinanceInvoiceAdminController(IFinanceDocumentAppService financeDocumentAppService)
    {
        _financeDocumentAppService = financeDocumentAppService;
    }

    [HttpPost("manual")]
    [Authorize(ZahyPermissions.Finance.WriteManualInvoice)]
    public async Task<ActionResult<FinanceDocumentDto>> CreateManualAsync(
        [FromBody] CreateManualInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await _financeDocumentAppService.CreateManualInvoiceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAsync), new { id = dto.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    [Authorize(ZahyPermissions.Finance.ReadAll)]
    public Task<FinanceDocumentDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _financeDocumentAppService.GetAsync(id, cancellationToken);

    [HttpGet("{id:guid}/pdf")]
    [Authorize(ZahyPermissions.Finance.ReadAll)]
    public async Task<IActionResult> GetPdfAsync(Guid id, CancellationToken cancellationToken)
    {
        var download = await _financeDocumentAppService.GetPdfAsync(id, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
