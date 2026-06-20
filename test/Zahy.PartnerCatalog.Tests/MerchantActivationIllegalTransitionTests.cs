using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class MerchantActivationIllegalTransitionTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly DateTime T = DateTime.UtcNow;

    [Fact]
    public async Task Pending_Cannot_Suspend()
    {
        var activation = await CreatePendingActivationAsync();

        Should.Throw<BusinessException>(() => activation.Suspend(T))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public async Task Pending_Can_End_Via_End_Or_Cancel()
    {
        var activation = await CreatePendingActivationAsync();
        activation.End(T);
        activation.Status.ShouldBe(MerchantActivationStatus.Ended);
    }

    [Fact]
    public async Task Ended_Cannot_Resume()
    {
        var activation = await CreatePendingActivationAsync();
        activation.Cancel(T);

        Should.Throw<BusinessException>(() => activation.Resume(T.AddHours(1)))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidStatusTransition);
    }

    private async Task<MerchantActivation> CreatePendingActivationAsync()
    {
        PartnerCatalogItem? item = null;

        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerId,
                "SVC-ILLEGAL",
                "Service",
                null,
                PartnerCatalogOfferingKind.ServiceOneOff,
                Money.Of(70m, vatInclusive: true));
            item.Publish(T);
            await itemRepo.InsertAsync(item, autoSave: true);
        });

        return MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item!,
            Money.Of(100m, vatInclusive: true));
    }
}

public sealed class PartnerCatalogTestCurrentPartner : ICurrentPartner
{
    public Guid? Id { get; set; }
}

public sealed class PartnerCatalogTestCurrentTenant : Volo.Abp.MultiTenancy.ICurrentTenant
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }

    public bool IsAvailable => Id.HasValue;

    public IDisposable Change(Guid? id, string? name = null)
    {
        var previousId = Id;
        var previousName = Name;
        Id = id;
        Name = name;
        return new RestoreScope(() =>
        {
            Id = previousId;
            Name = previousName;
        });
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly Action _restore;

        public RestoreScope(Action restore) => _restore = restore;

        public void Dispose() => _restore();
    }
}
