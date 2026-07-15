using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

/// <summary>
/// P5 test fakes — per-test-configurable counterparty + period lookups replacing the real
/// implementations (the real counterparty lookup reads PartnerPlatform/PartnerCatalog repositories,
/// which the Finance test host does not compose — same replace convention as TestCurrentPartner).
/// Defaults are ALL CLEAR so pre-P5 tests keep passing unchanged.
/// </summary>
public class FinanceTestCounterpartyLookup : IFinanceCounterpartyLookup
{
    public FinanceCounterpartySnapshot PartnerSnapshot { get; set; } = new(true, true, false);

    public FinanceCounterpartySnapshot MerchantSnapshot { get; set; } = new(true, true, false);

    public void Reset()
    {
        PartnerSnapshot = new FinanceCounterpartySnapshot(true, true, false);
        MerchantSnapshot = new FinanceCounterpartySnapshot(true, true, false);
    }

    public Task<FinanceCounterpartySnapshot> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PartnerSnapshot);

    public Task<FinanceCounterpartySnapshot> GetMerchantAsync(Guid merchantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(MerchantSnapshot);
}

/// <summary>Settable period provider — proves the :086 seam (default open, tests can close it).</summary>
public class FinanceTestPeriodStatusProvider : IFinancePeriodStatusProvider
{
    public bool Open { get; set; } = true;

    public Task<bool> IsPeriodOpenAsync(DateTime issueDateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(Open);
}
