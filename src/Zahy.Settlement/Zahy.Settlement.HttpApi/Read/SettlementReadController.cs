using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Settlement.Read;

[Route("api/settlement")]
public class SettlementReadController : AbpControllerBase
{
    private readonly ISettlementReadAppService _readAppService;

    public SettlementReadController(ISettlementReadAppService readAppService)
    {
        _readAppService = readAppService;
    }

    [HttpGet("cases")]
    public Task<List<SettlementCaseReadDto>> GetCasesAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetCasesAsync(new SettlementPartnerQuery { PartnerId = partnerId });

    [HttpGet("reversals")]
    public Task<List<SettlementCaseReadDto>> GetReversalsAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetReversalsAsync(new SettlementPartnerQuery { PartnerId = partnerId });

    [HttpGet("billing-charges")]
    public Task<List<SettlementBillingChargeReadDto>> GetBillingChargesAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetBillingChargesAsync(new SettlementPartnerQuery { PartnerId = partnerId });
}
