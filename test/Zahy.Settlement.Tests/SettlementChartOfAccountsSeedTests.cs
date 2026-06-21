using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement.Read;

namespace Zahy.Settlement;

/// <summary>
/// Integration test (in-memory SQLite, tables built from the model — no migration applied, no
/// DbMigrator). Proves the chart seed lands all ten accounts and is idempotent on re-seed.
/// </summary>
public class SettlementChartOfAccountsSeedTests : ZahySettlementReadTestBase
{
    private readonly SettlementChartOfAccountsDataSeedContributor _contributor;
    private readonly IRepository<LedgerAccount, Guid> _accounts;

    public SettlementChartOfAccountsSeedTests()
    {
        _contributor = GetRequiredService<SettlementChartOfAccountsDataSeedContributor>();
        _accounts = GetRequiredService<IRepository<LedgerAccount, Guid>>();
    }

    [Fact]
    public async Task Seeds_All_Eleven_Accounts_With_Correct_Type_And_NormalSide()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _contributor.SeedAsync(new DataSeedContext());

            var accounts = await _accounts.GetListAsync();
            accounts.Count.ShouldBe(11);

            accounts.Select(a => a.Code).OrderBy(c => c).ShouldBe(
                new[] { "1100", "1200", "1250", "1300", "2100", "2200", "2300", "2400", "4100", "4200", "5100" });

            // Every seeded row matches the canonical definition (type + normal side).
            foreach (var def in SettlementChartOfAccounts.All)
            {
                var seeded = accounts.Single(a => a.Code == def.Code);
                seeded.Name.ShouldBe(def.Name);
                seeded.Type.ShouldBe(def.Type);
                seeded.NormalSide.ShouldBe(def.NormalSide);
                seeded.Portfolio.ShouldBe(def.Portfolio);
            }
        });
    }

    [Fact]
    public async Task Seed_Is_Idempotent_ReSeed_Adds_Nothing()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _contributor.SeedAsync(new DataSeedContext());
            await _contributor.SeedAsync(new DataSeedContext());
            await _contributor.SeedAsync(new DataSeedContext());

            (await _accounts.GetCountAsync()).ShouldBe(11);
        });
    }

    [Fact]
    public async Task Seeded_Codes_Are_Unique()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _contributor.SeedAsync(new DataSeedContext());

            var codes = (await _accounts.GetListAsync()).Select(a => a.Code).ToList();
            codes.Distinct().Count().ShouldBe(codes.Count);
        });
    }
}
