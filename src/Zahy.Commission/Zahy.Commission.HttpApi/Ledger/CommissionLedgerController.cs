using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Commission.HttpApi.Ledger;

/// <summary>HTTP surface for the commission ledger approval workflow (Accrued → Approved → Paid).</summary>
[Route("api/commission/ledger")]
public class CommissionLedgerController : AbpControllerBase
{
    private readonly ICommissionLedgerService _ledgerService;

    public CommissionLedgerController(ICommissionLedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    [HttpGet]
    public Task<List<CommissionLedgerEntryDto>> GetListAsync([FromQuery] CommissionLedgerListInput input) =>
        _ledgerService.GetListAsync(input);

    [HttpPost("{entryId:guid}/approve")]
    public Task<CommissionLedgerEntryDto> ApproveAsync(Guid entryId) =>
        _ledgerService.ApproveAsync(entryId);

    [HttpPost("{entryId:guid}/reverse")]
    public Task<CommissionLedgerReversalResult> ReverseAsync(Guid entryId, [FromBody] ReverseCommissionLedgerInput input) =>
        _ledgerService.ReverseAsync(entryId, input);

    [HttpPost("{entryId:guid}/mark-paid")]
    public Task<CommissionLedgerEntryDto> MarkPaidAsync(Guid entryId) =>
        _ledgerService.MarkPaidAsync(entryId);
}
