using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Zahy.PartnerCatalog;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Finance;

/// <summary>
/// P5 — the REAL counterparty lookup, wired read-only from the owning modules: partner existence and
/// Active status from PartnerPlatform's <see cref="Partner"/>, and the derived ReflectionOnly
/// participation from PartnerCatalog (≥1 offering AND every offering ReflectionOnly → no money
/// relationship). Merchants are ABP tenants with no backend registry yet — the merchant answer is the
/// documented exists+active null-object (the FE mock enforces the real merchant check). Buildable and
/// registered by default; tests replace the interface with a fake (same convention as the
/// partner-type lookup precedent).
/// </summary>
public class PartnerPlatformFinanceCounterpartyLookup : IFinanceCounterpartyLookup, ITransientDependency
{
    private readonly IRepository<Partner, Guid> _partnerRepository;
    private readonly IRepository<PartnerCatalogItem, Guid> _catalogItemRepository;

    public PartnerPlatformFinanceCounterpartyLookup(
        IRepository<Partner, Guid> partnerRepository,
        IRepository<PartnerCatalogItem, Guid> catalogItemRepository)
    {
        _partnerRepository = partnerRepository;
        _catalogItemRepository = catalogItemRepository;
    }

    public async Task<FinanceCounterpartySnapshot> GetPartnerAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.FindAsync(partnerId, cancellationToken: cancellationToken);
        if (partner == null)
        {
            return FinanceCounterpartySnapshot.Missing;
        }

        var items = await _catalogItemRepository.GetListAsync(
            x => x.PartnerId == partnerId,
            cancellationToken: cancellationToken);
        var reflectionOnly = items.Count > 0 &&
                             items.All(x => x.SettlementParticipationMode == SettlementParticipationMode.ReflectionOnly);

        return new FinanceCounterpartySnapshot(
            Exists: true,
            IsActive: partner.Status == PartnerStatus.Active,
            IsReflectionOnly: reflectionOnly);
    }

    public Task<FinanceCounterpartySnapshot> GetMerchantAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default) =>
        // No backend merchant registry (merchants = tenants): documented exists+active null-object.
        Task.FromResult(new FinanceCounterpartySnapshot(Exists: true, IsActive: true, IsReflectionOnly: false));
}
