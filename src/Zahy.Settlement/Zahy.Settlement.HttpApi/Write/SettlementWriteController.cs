using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.Settlement.Read;
using Zahy.Settlement.Write;

namespace Zahy.Settlement.HttpApi.Write;

/// <summary>HTTP surface for operator-triggered settlement corrections (reversals).</summary>
[Route("api/settlement")]
public class SettlementWriteController : AbpControllerBase
{
    private readonly ISettlementWriteAppService _writeAppService;

    public SettlementWriteController(ISettlementWriteAppService writeAppService)
    {
        _writeAppService = writeAppService;
    }

    [HttpPost("reversals")]
    public Task<SettlementCaseReadDto> TriggerCaseReversalAsync([FromBody] TriggerReversalRequest request) =>
        _writeAppService.TriggerCaseReversalAsync(request);
}
