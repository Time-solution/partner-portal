using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

/// <summary>Read-only counterparty facts the P5 gate needs (:084 / :088).</summary>
public sealed record FinanceCounterpartySnapshot(bool Exists, bool IsActive, bool IsReflectionOnly)
{
    public static FinanceCounterpartySnapshot Missing { get; } = new(false, false, false);
}

/// <summary>
/// P5 — read-only counterparty lookup for the pre-invoice gate. The REAL implementation is wired from
/// the owning modules (PartnerPlatform for partner existence/status; PartnerCatalog for the derived
/// ReflectionOnly participation — a partner with ≥1 offering, all ReflectionOnly, has no money
/// relationship). Merchants have no backend registry yet (they are ABP tenants), so the merchant
/// answer is the documented exists+active null-object until one lands; the FE mock enforces the real
/// merchant check against its own data.
/// </summary>
public interface IFinanceCounterpartyLookup
{
    Task<FinanceCounterpartySnapshot> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken = default);

    Task<FinanceCounterpartySnapshot> GetMerchantAsync(Guid merchantId, CancellationToken cancellationToken = default);
}
