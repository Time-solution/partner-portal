using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement.Read;

namespace Zahy.Settlement;

/// <summary>
/// Integration test (in-memory SQLite, tables from the model — no migration applied, no DbMigrator).
/// ReflectionOnly must post ZERO financial ledger rows and write exactly ONE reflection-log row.
/// </summary>
public class SettlementPostingReflectionTests : ZahySettlementReadTestBase
{
    private readonly SettlementPostingService _service;
    private readonly IRepository<ReflectionLog, Guid> _reflectionLogs;

    public SettlementPostingReflectionTests()
    {
        _service = GetRequiredService<SettlementPostingService>();
        _reflectionLogs = GetRequiredService<IRepository<ReflectionLog, Guid>>();
    }

    [Fact]
    public async Task ReflectionOnly_Posts_No_Journal_And_Writes_One_Log_Row()
    {
        var merchantId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _service.ReflectAsync(
                "ord-901",
                Money.Of(113.00m, "SAR", vatInclusive: true),
                merchantId,
                DateTime.UtcNow);

            // Zero financial ledger rows.
            result.Mode.ShouldBe(ParticipationMode.ReflectionOnly);
            result.IsFinancial.ShouldBeFalse();
            result.Lines.Count.ShouldBe(0);
            result.IsBalanced.ShouldBeTrue();

            // Exactly one reflection-log row.
            (await _reflectionLogs.GetCountAsync()).ShouldBe(1);

            var log = (await _reflectionLogs.GetListAsync())[0];
            log.OrderReference.ShouldBe("ord-901");
            log.Gross.Amount.ShouldBe(113.00m);
            log.MerchantId.ShouldBe(merchantId);
        });
    }
}
