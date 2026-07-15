using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;

namespace Zahy.Finance;

public interface ISqlServerFinanceInvoiceNumberAllocator
{
    Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default);

    Task<FinanceInvoiceNumberAllocation> AllocateManualInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default);
}

public class SqlServerFinanceInvoiceNumberAllocator : ISqlServerFinanceInvoiceNumberAllocator
{
    private readonly IRepository<InvoiceNumberSequence, Guid> _sequenceRepository;
    private readonly IDbContextProvider<ZahyFinanceDbContext> _dbContextProvider;
    private readonly IGuidGenerator _guidGenerator;

    public SqlServerFinanceInvoiceNumberAllocator(
        IRepository<InvoiceNumberSequence, Guid> sequenceRepository,
        IDbContextProvider<ZahyFinanceDbContext> dbContextProvider,
        IGuidGenerator guidGenerator)
    {
        _sequenceRepository = sequenceRepository;
        _dbContextProvider = dbContextProvider;
        _guidGenerator = guidGenerator;
    }

    public Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(FinanceDocumentKind.Invoice, issueDate, FinanceInvoiceNumberFormat.Format, cancellationToken);

    /// <summary>P4 — MAN pool under the same UPDLOCK/ROWLOCK serialization as the fiscal pool.</summary>
    public Task<FinanceInvoiceNumberAllocation> AllocateManualInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(FinanceDocumentKind.ManualInvoice, issueDate, FinanceInvoiceNumberFormat.FormatManual, cancellationToken);

    private async Task<FinanceInvoiceNumberAllocation> AllocateAsync(
        FinanceDocumentKind sequenceKind,
        DateTime issueDate,
        Func<int, int, string> format,
        CancellationToken cancellationToken)
    {
        var fiscalYear = issueDate.Year;
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        var sequences = await dbContext.InvoiceNumberSequences
            .FromSqlRaw(
                "SELECT * FROM FinInvoiceNumberSequences WITH (UPDLOCK, ROWLOCK) WHERE DocumentKind = {0} AND FiscalYear = {1}",
                (int)sequenceKind,
                fiscalYear)
            .ToListAsync(cancellationToken);

        InvoiceNumberSequence sequence;
        if (sequences.Count == 0)
        {
            sequence = InvoiceNumberSequence.Create(
                _guidGenerator.Create(),
                sequenceKind,
                fiscalYear);
            await _sequenceRepository.InsertAsync(sequence, autoSave: true, cancellationToken: cancellationToken);
        }
        else
        {
            sequence = sequences[0];
        }

        var sequenceNumber = sequence.AllocateNext();
        await _sequenceRepository.UpdateAsync(sequence, autoSave: true, cancellationToken: cancellationToken);

        return new FinanceInvoiceNumberAllocation
        {
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            InvoiceNumber = format(fiscalYear, sequenceNumber)
        };
    }
}
