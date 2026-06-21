using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Settlement;

/// <summary>
/// Seeds the canonical settlement Chart of Accounts (<see cref="SettlementChartOfAccounts"/>).
/// This is reference data — seeded in every environment (not gated behind any dev flag).
/// Idempotent: an account is inserted only when its code is absent, so re-seeding adds nothing.
/// </summary>
public class SettlementChartOfAccountsDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<LedgerAccount, Guid> _accounts;
    private readonly IGuidGenerator _guidGenerator;

    public SettlementChartOfAccountsDataSeedContributor(
        IRepository<LedgerAccount, Guid> accounts,
        IGuidGenerator guidGenerator)
    {
        _accounts = accounts;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        foreach (var def in SettlementChartOfAccounts.All)
        {
            var existing = await _accounts.FindAsync(a => a.Code == def.Code);
            if (existing != null)
            {
                continue;
            }

            await _accounts.InsertAsync(
                new LedgerAccount(
                    _guidGenerator.Create(),
                    def.Code,
                    def.Name,
                    def.Type,
                    def.NormalSide,
                    def.Portfolio),
                autoSave: true);
        }
    }
}
