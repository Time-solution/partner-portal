using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Zahy.Identity.Permissions;

namespace Zahy.Settlement.BankRegistry;

/// <summary>
/// Write side of the accountant-managed bank registry (Track B). Establishes the first Settlement write
/// app service. Adding a bank assigns the next free 110x sub-account under 1100 — this is REGISTRY data
/// only; it does NOT mutate the production chart of accounts (that live change stays gated, see
/// <c>SettlementEngineOptions.BankRegistryLiveChartEnabled</c>). Gated to accountant + platform admin.
/// </summary>
[Authorize(ZahyPermissions.Settlement.BankRegistryManage)]
public class BankAccountAppService : ApplicationService, IBankAccountAppService
{
    private readonly IRepository<BankAccount, Guid> _repository;

    public BankAccountAppService(IRepository<BankAccount, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<List<BankAccountDto>> GetListAsync()
    {
        var banks = await _repository.GetListAsync();
        return banks
            .OrderBy(b => b.Code, StringComparer.Ordinal)
            .Select(Map)
            .ToList();
    }

    public async Task<BankAccountDto> CreateAsync(CreateBankAccountInput input)
    {
        var existing = await _repository.GetListAsync();
        var code = BankLedgerCoding.NextCode(existing.Select(b => b.Code));

        var bank = new BankAccount(
            GuidGenerator.Create(),
            code,
            input.Name,
            input.AccountNumber,
            input.Currency,
            input.GatewayMapping);

        await _repository.InsertAsync(bank, autoSave: true);
        return Map(bank);
    }

    public async Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountInput input)
    {
        var bank = await _repository.GetAsync(id);
        bank.Update(input.Name, input.AccountNumber, input.Currency, input.GatewayMapping);
        await _repository.UpdateAsync(bank, autoSave: true);
        return Map(bank);
    }

    public async Task<BankAccountDto> DeactivateAsync(Guid id)
    {
        var bank = await _repository.GetAsync(id);
        bank.Deactivate();
        await _repository.UpdateAsync(bank, autoSave: true);
        return Map(bank);
    }

    public async Task<BankAccountDto> ActivateAsync(Guid id)
    {
        var bank = await _repository.GetAsync(id);
        bank.Activate();
        await _repository.UpdateAsync(bank, autoSave: true);
        return Map(bank);
    }

    private static BankAccountDto Map(BankAccount bank) => new()
    {
        Id = bank.Id,
        Code = bank.Code,
        ParentCode = BankLedgerCoding.ParentCode,
        Name = bank.Name,
        MaskedAccountNumber = bank.MaskedAccountNumber,
        Currency = bank.Currency,
        Status = bank.Status,
        GatewayMapping = bank.GatewayMapping,
    };
}
