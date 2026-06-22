using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.Settlement.BankRegistry;

namespace Zahy.Settlement.HttpApi.BankRegistry;

/// <summary>HTTP surface for the accountant-managed bank registry (Track B).</summary>
[Route("api/settlement/bank-accounts")]
public class BankAccountController : AbpControllerBase
{
    private readonly IBankAccountAppService _bankAccountAppService;

    public BankAccountController(IBankAccountAppService bankAccountAppService)
    {
        _bankAccountAppService = bankAccountAppService;
    }

    [HttpGet]
    public Task<List<BankAccountDto>> GetListAsync() => _bankAccountAppService.GetListAsync();

    [HttpPost]
    public Task<BankAccountDto> CreateAsync([FromBody] CreateBankAccountInput input) =>
        _bankAccountAppService.CreateAsync(input);

    [HttpPut("{id}")]
    public Task<BankAccountDto> UpdateAsync(Guid id, [FromBody] UpdateBankAccountInput input) =>
        _bankAccountAppService.UpdateAsync(id, input);

    [HttpPost("{id}/deactivate")]
    public Task<BankAccountDto> DeactivateAsync(Guid id) => _bankAccountAppService.DeactivateAsync(id);

    [HttpPost("{id}/activate")]
    public Task<BankAccountDto> ActivateAsync(Guid id) => _bankAccountAppService.ActivateAsync(id);
}
