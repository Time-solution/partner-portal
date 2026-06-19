using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

public interface IFinanceAccountService
{
    Task<FinanceAccountOpenResult> OpenPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);

    Task<FinanceAccountOpenResult> OpenMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public interface IFinanceAccountQueryService
{
    Task<FinanceAccountBalanceDto> GetPartnerBalanceAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);

    Task<FinanceAccountBalanceDto> GetMerchantBalanceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);

    Task EnsureCanAccessMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class FinanceAccountOpenResult
{
    public Guid AccountId { get; init; }

    public FinanceAccountKind AccountKind { get; init; }

    public bool IsNew { get; init; }

    public FinanceAccountStatus Status { get; init; }
}

public sealed class FinanceAccountBalanceDto
{
    public Guid AccountId { get; init; }

    public FinanceAccountKind AccountKind { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = FinanceConsts.DefaultCurrency;

    public FinanceAccountStatus Status { get; init; }
}
