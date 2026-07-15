using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

/// <summary>
/// Host/runtime adapter: delegates gapless numbering to the SQL Server UPDLOCK allocator.
/// </summary>
public class SqlServerFinanceInvoiceNumberAllocatorAdapter : IFinanceInvoiceNumberAllocator
{
    private readonly ISqlServerFinanceInvoiceNumberAllocator _sqlServerAllocator;

    public SqlServerFinanceInvoiceNumberAllocatorAdapter(
        ISqlServerFinanceInvoiceNumberAllocator sqlServerAllocator)
    {
        _sqlServerAllocator = sqlServerAllocator;
    }

    public Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default) =>
        _sqlServerAllocator.AllocateInvoiceNumberAsync(issueDate, cancellationToken);

    public Task<FinanceInvoiceNumberAllocation> AllocateManualInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default) =>
        _sqlServerAllocator.AllocateManualInvoiceNumberAsync(issueDate, cancellationToken);
}
