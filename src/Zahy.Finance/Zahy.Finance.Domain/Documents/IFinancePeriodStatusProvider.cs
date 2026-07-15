using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Zahy.Finance;

/// <summary>
/// P5 SEAM — answers whether the accounting period covering an issue date is OPEN. The default is the
/// null-object below (everything open — same convention as the partner-type lookup): a REAL
/// period-lock provider is accountant-gated and slots in by replacing this registration. The gate
/// consumes the answer via <see cref="FinanceInvoiceGateContext.PeriodOpen"/> (:086). No close/lock
/// logic exists here by design.
/// </summary>
public interface IFinancePeriodStatusProvider
{
    Task<bool> IsPeriodOpenAsync(DateTime issueDateUtc, CancellationToken cancellationToken = default);
}

/// <summary>Null-object default: every period is open until the accountant-gated provider lands.</summary>
public class OpenFinancePeriodStatusProvider : IFinancePeriodStatusProvider, ITransientDependency
{
    public Task<bool> IsPeriodOpenAsync(DateTime issueDateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
