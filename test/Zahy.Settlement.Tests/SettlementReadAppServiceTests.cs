using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Commission;
using Zahy.PartnerCatalog;

namespace Zahy.Settlement.Read;

public class SettlementReadAppServiceTests : ZahySettlementReadTestBase
{
    private static readonly Guid PartnerA = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid PartnerB = Guid.Parse("33333333-3333-3333-3333-333333333003");

    [Fact]
    public async Task Should_Return_Case_With_Journal_Lines()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var caseRepo = GetRequiredService<IRepository<SettlementCase, Guid>>();
            var eventStore = GetRequiredService<ISettlementEventStore>();

            var settlementCase = SettlementCase.Start(
                Guid.NewGuid(),
                SettlementBook.Marketplace,
                PartnerA,
                "order:ord-7001:v1",
                DateTime.UtcNow);
            settlementCase.TransitionTo(SettlementCaseState.Allocated, DateTime.UtcNow);
            await caseRepo.InsertAsync(settlementCase, autoSave: true);

            var allocation = SettlementAllocationSnapshot.From(
                new SettlementAllocationResult
                {
                    Journal = Journal.Create(
                        Guid.NewGuid(),
                        DateTime.UtcNow,
                        new[]
                        {
                            JournalLine.Debit(SettlementAccountType.AggregatorClearing, Money.Of(13m)),
                            JournalLine.Credit(SettlementAccountType.DeliveryCost, Money.Of(10m)),
                            JournalLine.Credit(SettlementAccountType.ShippingMarginRevenue, Money.Of(3m)),
                        }),
                    MerchantPayout = Money.Of(0m),
                    PlatformCommissionNet = Money.Of(3m),
                    DeliveryCost = Money.Of(10m),
                    VatOutput = Money.Of(0m),
                    VatInput = Money.Of(0m),
                    NetVatToZatca = Money.Of(0m),
                });

            await eventStore.InsertAsync(
                SettlementWebhookEvent.Processed(
                    Guid.NewGuid(),
                    SettlementBook.Marketplace,
                    PartnerA,
                    "evt-1",
                    "{}",
                    WebhookSignatureStatus.Valid,
                    SettlementEventOutcome.Allocated,
                    settlementCase.Id,
                    SettlementCaseState.Allocated,
                    allocation.ToJson(),
                    DateTime.UtcNow));

            var read = GetRequiredService<ISettlementReadAppService>();
            var dtos = await read.GetCasesAsync(new SettlementPartnerQuery { PartnerId = PartnerA });

            dtos.Count.ShouldBe(1);
            var dto = dtos.Single();
            dto.Journal.ShouldNotBeNull();
            dto.Journal!.Lines.Count.ShouldBe(3);
            dto.Journal.TotalDebits.Amount.ShouldBe(13m);
            dto.Journal.TotalCredits.Amount.ShouldBe(13m);
            dto.Journal.Lines.First().Account.ShouldBe(SettlementAccountType.AggregatorClearing);
        });
    }

    [Fact]
    public async Task Should_Return_Reversal_Cases_Separately()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var caseRepo = GetRequiredService<IRepository<SettlementCase, Guid>>();

            var original = SettlementCase.Start(
                Guid.NewGuid(),
                SettlementBook.Marketplace,
                PartnerA,
                "order:orig:v1",
                DateTime.UtcNow);
            await caseRepo.InsertAsync(original, autoSave: true);

            var reversal = SettlementCase.Start(
                Guid.NewGuid(),
                SettlementBook.Marketplace,
                PartnerA,
                "order:rev:v1",
                DateTime.UtcNow);
            reversal.LinkReversal(original.Id);
            await caseRepo.InsertAsync(reversal, autoSave: true);

            var read = GetRequiredService<ISettlementReadAppService>();
            (await read.GetCasesAsync(new SettlementPartnerQuery { PartnerId = PartnerA })).Count.ShouldBe(1);
            var reversals = await read.GetReversalsAsync(new SettlementPartnerQuery { PartnerId = PartnerA });
            reversals.Count.ShouldBe(1);
            reversals.Single().ReversesSettlementCaseId.ShouldBe(original.Id);
        });
    }

    [Fact]
    public async Task Partner_Filter_Blocks_Cross_Partner_Settlement_Read()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var caseRepo = GetRequiredService<IRepository<SettlementCase, Guid>>();
            await caseRepo.InsertAsync(
                SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, PartnerA, "a-1", DateTime.UtcNow),
                autoSave: true);
            await caseRepo.InsertAsync(
                SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, PartnerB, "b-1", DateTime.UtcNow),
                autoSave: true);

            GetRequiredService<SettlementReadTestCurrentPartner>().Id = PartnerA;
            var read = GetRequiredService<ISettlementReadAppService>();

            (await read.GetCasesAsync(new SettlementPartnerQuery())).Count.ShouldBe(1);
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                read.GetCasesAsync(new SettlementPartnerQuery { PartnerId = PartnerB }));
        });
    }
}
