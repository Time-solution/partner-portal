using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Finance;

/// <summary>
/// GET /api/finance/vat-export — per-transaction VAT export (CSV / XLSX). Authorization
/// (Finance.ReadAll) is enforced on <see cref="IFinanceVatExportAppService"/>.
/// </summary>
[Route("api/finance/vat-export")]
public class FinanceVatExportController : AbpControllerBase
{
    private readonly IFinanceVatExportAppService _vatExportAppService;

    public FinanceVatExportController(IFinanceVatExportAppService vatExportAppService)
    {
        _vatExportAppService = vatExportAppService;
    }

    [HttpGet]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] string? format,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? partnerId,
        [FromQuery] Guid? merchantId,
        CancellationToken cancellationToken = default)
    {
        var request = new FinanceVatExportRequest
        {
            Format = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
                ? FinanceExportFormat.Xlsx
                : FinanceExportFormat.Csv,
            From = from,
            To = to,
            PartnerId = partnerId,
            MerchantId = merchantId
        };

        var download = await _vatExportAppService.ExportVatTransactionsAsync(request, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName);
    }
}
