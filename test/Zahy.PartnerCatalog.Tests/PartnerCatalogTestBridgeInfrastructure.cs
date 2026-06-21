using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Zahy.Commission;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>Mutable merchant options for integration tests (ParticipationBridgeEnabled toggle).</summary>
public sealed class PartnerCatalogTestMerchantOptions : IOptionsMonitor<PartnerCatalogMerchantOptions>, ISingletonDependency
{
    private PartnerCatalogMerchantOptions _current = new();

    public PartnerCatalogMerchantOptions CurrentValue => _current;

    public PartnerCatalogMerchantOptions Get(string? name) => _current;

    public void Reset() => _current = new PartnerCatalogMerchantOptions();

    public void Configure(Action<PartnerCatalogMerchantOptions> configure)
    {
        configure(_current);
    }

    public IDisposable? OnChange(Action<PartnerCatalogMerchantOptions, string?> listener) => null;
}

/// <summary>In-memory settlement case store — Partner Catalog tests have no Settlement EF.</summary>
public sealed class PartnerCatalogTestSettlementCaseStore : ISettlementCaseStore, ISingletonDependency
{
    public readonly List<SettlementCase> Cases = new();

    public void Reset() => Cases.Clear();

    public Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string ext, CancellationToken ct = default) =>
        Task.FromResult(Cases.FirstOrDefault(c => c.Book == book && c.ExternalTransactionId == ext));

    public Task InsertAsync(SettlementCase c, CancellationToken ct = default)
    {
        Cases.Add(c);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SettlementCase c, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>In-memory billing charges — Commission EF is not loaded in Partner Catalog tests.</summary>
public sealed class PartnerCatalogTestBillingChargeService : IBillingChargeService, ISingletonDependency
{
    public readonly List<BillingChargeRequest> Requests = new();
    private readonly Dictionary<string, BillingChargeResult> _byKey = new();

    public void Reset()
    {
        Requests.Clear();
        _byKey.Clear();
    }

    public Task<BillingChargeResult> ChargeAsync(
        BillingChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_byKey.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return Task.FromResult(new BillingChargeResult
            {
                ChargeId = existing.ChargeId,
                IsNew = false,
                Kind = existing.Kind,
                ChargeTarget = existing.ChargeTarget,
                Amount = existing.Amount,
                IdempotencyKey = existing.IdempotencyKey
            });
        }

        var created = new BillingChargeResult
        {
            ChargeId = Guid.NewGuid(),
            IsNew = true,
            Kind = request.Kind,
            ChargeTarget = request.ChargeTarget,
            Amount = request.Amount,
            IdempotencyKey = request.IdempotencyKey
        };
        _byKey[request.IdempotencyKey] = created;
        Requests.Add(request);
        return Task.FromResult(created);
    }

    public Task<BillingChargeResult> ChargeCommissionAccrualAsync(
        CommissionLedgerAccrualResult accrual,
        Guid partnerId,
        Guid? tenantId,
        string currency,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<BillingChargeResult> ChargeActivationIfConfiguredAsync(
        Guid partnerId,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
