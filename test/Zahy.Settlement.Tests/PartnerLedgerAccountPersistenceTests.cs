using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement.Read;

namespace Zahy.Settlement;

/// <summary>
/// DB round-trip for the per-partner ledger registry. Proves a partner's payable (2101+) and receivable
/// (1251+) sub-accounts survive a real save/load, that multiple 3PLs persist distinct sub-account pairs,
/// and that the registrar re-resolves an existing partner idempotently from persisted rows. REGISTRY
/// data only — nothing is posted (PostingEnabled OFF) and the production chart is untouched.
/// </summary>
public class PartnerLedgerAccountPersistenceTests : ZahySettlementReadTestBase
{
    private readonly IRepository<PartnerLedgerAccount, Guid> _registry;

    public PartnerLedgerAccountPersistenceTests()
    {
        _registry = GetRequiredService<IRepository<PartnerLedgerAccount, Guid>>();
    }

    [Fact]
    public async Task A_Partner_Sub_Account_Pair_Persists()
    {
        var id = Guid.NewGuid();
        var partner = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await _registry.InsertAsync(
                new PartnerLedgerAccount(id, partner, "Salasa 3PL", "2101", "1251"), autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var loaded = await _registry.GetAsync(id);
            loaded.PartnerId.ShouldBe(partner);
            loaded.PayableCode.ShouldBe("2101");
            loaded.ReceivableCode.ShouldBe("1251");
            loaded.Status.ShouldBe(PartnerLedgerAccountStatus.Active);
        });
    }

    [Fact]
    public async Task Multi_3PL_Persist_Distinct_Sub_Account_Pairs_And_Resolve_Idempotently()
    {
        var salasa = Guid.NewGuid();
        var oto = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var existing = await _registry.GetListAsync();
            var salasaRow = PartnerLedgerAccounts.EnsureFor(Guid.NewGuid(), salasa, "Salasa", existing);
            await _registry.InsertAsync(salasaRow, autoSave: true);

            existing = await _registry.GetListAsync();
            var otoRow = PartnerLedgerAccounts.EnsureFor(Guid.NewGuid(), oto, "Oto", existing);
            await _registry.InsertAsync(otoRow, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var rows = await _registry.GetListAsync();
            rows.Count.ShouldBe(2);

            rows.Single(r => r.PartnerId == salasa).PayableCode.ShouldBe("2101");
            rows.Single(r => r.PartnerId == oto).PayableCode.ShouldBe("2102");
            rows.Single(r => r.PartnerId == salasa).ReceivableCode.ShouldBe("1251");
            rows.Single(r => r.PartnerId == oto).ReceivableCode.ShouldBe("1252");

            // Re-ensuring an already-registered partner returns the SAME persisted row (no new code).
            var again = PartnerLedgerAccounts.EnsureFor(Guid.NewGuid(), salasa, "Salasa", rows);
            again.PayableCode.ShouldBe("2101");
        });
    }
}
