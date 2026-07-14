using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Commission;
using Zahy.PartnerCatalog.Merchant;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class MerchantActivationSnapshotOrchestrationTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111001");

    [Fact]
    public async Task Activate_Flag_Off_Preserves_Phase2_No_Snapshot_No_Bridge()
    {
        ResetBridgeTestState();
        GetRequiredService<PartnerCatalogTestMerchantOptions>().Reset();

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertPrincipalServiceItemAsync("ORCH-OFF-1");
            SetTenant(TenantA);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var cases = GetRequiredService<PartnerCatalogTestSettlementCaseStore>();
            var before = await snapshotRepo.GetCountAsync();

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            dto.Status.ShouldBe(MerchantActivationStatus.Active);
            (await snapshotRepo.GetCountAsync()).ShouldBe(before);
            cases.Cases.Count.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Activate_Flag_On_Principal_ServiceOneOff_Creates_Snapshot_And_Dispatches_Once()
    {
        ResetBridgeTestState();
        EnableParticipationBridge();

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertPrincipalServiceItemAsync("ORCH-PRIN-1", buy: 70m);
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 85m, Currency = "SAR", VatInclusive = true },
            });

            dto.Status.ShouldBe(MerchantActivationStatus.Active);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var snapshots = await snapshotRepo.GetListAsync(x => x.MerchantActivationId == dto.Id);
            snapshots.Count.ShouldBe(1);

            var snapshot = snapshots.Single();
            snapshot.BuyPrice.Amount.ShouldBe(70m);
            snapshot.SellPrice.Amount.ShouldBe(85m);
            snapshot.MerchantActivationId.ShouldBe(dto.Id);
            snapshot.SettlementCaseId.ShouldNotBeNull();

            var cases = GetRequiredService<PartnerCatalogTestSettlementCaseStore>();
            cases.Cases.Count.ShouldBe(1);
            cases.Cases[0].ExternalTransactionId.ShouldBe(snapshot.ExternalTransactionId);
        });
    }

    [Fact]
    public async Task Activate_Flag_On_ReflectionOnly_Creates_Reflection_No_Settlement_Case()
    {
        ResetBridgeTestState();
        EnableParticipationBridge();

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertReflectionFnBItemAsync("ORCH-REF-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 35m, Currency = "SAR", VatInclusive = true },
            });

            dto.Status.ShouldBe(MerchantActivationStatus.Active);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var snapshot = (await snapshotRepo.GetListAsync(x => x.MerchantActivationId == dto.Id)).Single();
            snapshot.SettlementCaseId.ShouldBeNull();

            var reflectedRepo = GetRequiredService<IRepository<ReflectedPartnerOrder, Guid>>();
            var reflected = await reflectedRepo.GetListAsync(x => x.MerchantActivationId == dto.Id);
            reflected.Count.ShouldBe(1);
            reflected[0].SettlementCostMarkupSnapshotId.ShouldBe(snapshot.Id);

            GetRequiredService<PartnerCatalogTestSettlementCaseStore>().Cases.Count.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Activate_Flag_On_SubscriptionFee_With_BillingEnabled_Charges_Merchant_Fee()
    {
        ResetBridgeTestState();
        EnableParticipationBridge(subscriptionBilling: true);

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertSubscriptionFeeItemAsync("ORCH-SUB-1", fee: 115m);
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 115m, Currency = "SAR", VatInclusive = true },
            });

            dto.Status.ShouldBe(MerchantActivationStatus.Active);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var snapshot = (await snapshotRepo.GetListAsync(x => x.MerchantActivationId == dto.Id)).Single();
            snapshot.BillingChargeId.ShouldNotBeNull();
            snapshot.SettlementCaseId.ShouldBeNull();

            var billing = GetRequiredService<PartnerCatalogTestBillingChargeService>();
            billing.Requests.Count.ShouldBe(1);
            billing.Requests[0].Amount.ShouldBe(115m);
            billing.Requests[0].ChargeTarget.ShouldBe(BillingChargeTarget.Merchant);
            billing.Requests[0].Kind.ShouldBe(BillingChargeKind.Subscription);

            GetRequiredService<PartnerCatalogTestSettlementCaseStore>().Cases.Count.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Activate_Flag_On_Same_Tier_Twice_Is_Idempotent_One_Snapshot_One_Dispatch()
    {
        ResetBridgeTestState();
        EnableParticipationBridge();

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertPrincipalServiceItemAsync("ORCH-IDEM-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var first = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 85m, Currency = "SAR", VatInclusive = true },
            });
            var second = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            second.Id.ShouldBe(first.Id);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            (await snapshotRepo.CountAsync(x => x.MerchantActivationId == first.Id)).ShouldBe(1);

            GetRequiredService<PartnerCatalogTestSettlementCaseStore>().Cases.Count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task End_Then_Reactivate_Flag_On_Yields_Two_Snapshots_And_Two_Settlement_Cases()
    {
        // Ratified rule (CAT-FIX): a re-activation is a NEW activation — new sequence-suffixed key →
        // new snapshot (orchestrator dedup is per MerchantActivationId) → new settlement case.
        ResetBridgeTestState();
        EnableParticipationBridge();

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertPrincipalServiceItemAsync("ORCH-REACT-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var first = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 85m, Currency = "SAR", VatInclusive = true },
            });
            await merchant.DeactivateAsync(first.Id);

            var second = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 90m, Currency = "SAR", VatInclusive = true },
            });
            second.Id.ShouldNotBe(first.Id);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var firstSnapshot = await snapshotRepo.GetAsync(x => x.MerchantActivationId == first.Id);
            var secondSnapshot = await snapshotRepo.GetAsync(x => x.MerchantActivationId == second.Id);

            // Exactly one snapshot per activation cycle, each frozen at its own cycle's sell price.
            (await snapshotRepo.CountAsync(x =>
                x.PartnerCatalogItemId == item.Id && x.TenantId == TenantA)).ShouldBe(2);
            firstSnapshot.ExternalTransactionId.ShouldNotBe(secondSnapshot.ExternalTransactionId);
            firstSnapshot.SellPrice.Amount.ShouldBe(85m);
            secondSnapshot.SellPrice.Amount.ShouldBe(90m);

            var cases = GetRequiredService<PartnerCatalogTestSettlementCaseStore>().Cases;
            cases.Count.ShouldBe(2);
            cases.Select(c => c.ExternalTransactionId).Distinct().Count().ShouldBe(2);
        });
    }

    [Fact]
    public void Settlement_Engine_Posting_And_Disbursement_Remain_Off_By_Default()
    {
        var engineOptions = new SettlementEngineOptions();
        engineOptions.PostingEnabled.ShouldBeFalse();
        engineOptions.DisbursementEnabled.ShouldBeFalse();

        var merchantOptions = new PartnerCatalogMerchantOptions();
        merchantOptions.ParticipationBridgeEnabled.ShouldBeFalse();
        merchantOptions.SubscriptionBillingEnabled.ShouldBeFalse();
    }

    private void ResetBridgeTestState()
    {
        GetRequiredService<PartnerCatalogTestMerchantOptions>().Reset();
        GetRequiredService<PartnerCatalogTestSettlementCaseStore>().Reset();
        GetRequiredService<PartnerCatalogTestBillingChargeService>().Reset();
    }

    private void EnableParticipationBridge(bool subscriptionBilling = false)
    {
        GetRequiredService<PartnerCatalogTestMerchantOptions>().Configure(o =>
        {
            o.ParticipationBridgeEnabled = true;
            o.SubscriptionBillingEnabled = subscriptionBilling;
        });
    }

    private void SetTenant(Guid tenantId) =>
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = tenantId;

    private async Task<PartnerCatalogItem> InsertPrincipalServiceItemAsync(
        string code,
        decimal buy = 70m)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(buy, vatInclusive: true));
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }

    private async Task<PartnerCatalogItem> InsertReflectionFnBItemAsync(string code)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-ORCH",
            settlementParticipationMode: SettlementParticipationMode.ReflectionOnly);
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }

    private async Task<PartnerCatalogItem> InsertSubscriptionFeeItemAsync(string code, decimal fee)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.ServiceSubscription,
            Money.Of(70m, vatInclusive: true),
            settlementParticipationMode: SettlementParticipationMode.SubscriptionFee);
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }
}
