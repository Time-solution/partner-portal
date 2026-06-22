using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.Settlement.BankRegistry;

/// <summary>
/// Accountant-managed bank registry (Track B). Add/edit/deactivate banks; each new bank is auto-assigned
/// the next 110x ledger sub-account under the 1100 parent. Gated to the accountant (Platform.Finance) +
/// Platform.SuperAdmin via <c>ZahyPermissions.Settlement.BankRegistryManage</c>.
/// </summary>
public interface IBankAccountAppService : IApplicationService
{
    Task<List<BankAccountDto>> GetListAsync();

    Task<BankAccountDto> CreateAsync(CreateBankAccountInput input);

    Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountInput input);

    Task<BankAccountDto> DeactivateAsync(Guid id);

    Task<BankAccountDto> ActivateAsync(Guid id);
}
