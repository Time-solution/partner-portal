using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Zahy.Finance;

public class FinanceInvoiceNumberAllocator : ApplicationService, IFinanceInvoiceNumberAllocator
{
    private readonly IRepository<InvoiceNumberSequence, Guid> _sequenceRepository;
    private readonly IGuidGenerator _guidGenerator;

    public FinanceInvoiceNumberAllocator(
        IRepository<InvoiceNumberSequence, Guid> sequenceRepository,
        IGuidGenerator guidGenerator)
    {
        _sequenceRepository = sequenceRepository;
        _guidGenerator = guidGenerator;
    }

    public Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default) =>
        AllocateAsync(FinanceDocumentKind.Invoice, issueDate, FinanceInvoiceNumberFormat.Format, cancellationToken);

    /// <summary>P4 — manual invoices draw from their OWN sequence pool (MAN-yyyy-####, internal and
    /// non-fiscal). Same UoW semantics as the ledger pool: a rolled-back create releases the number,
    /// so the sequence stays gap-free.</summary>
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
        var queryable = await _sequenceRepository.GetQueryableAsync();
        var sequence = queryable.FirstOrDefault(x =>
            x.DocumentKind == sequenceKind &&
            x.FiscalYear == fiscalYear);

        if (sequence == null)
        {
            sequence = InvoiceNumberSequence.Create(
                _guidGenerator.Create(),
                sequenceKind,
                fiscalYear);
            await _sequenceRepository.InsertAsync(sequence, autoSave: false, cancellationToken: cancellationToken);
        }

        var sequenceNumber = sequence.AllocateNext();
        await _sequenceRepository.UpdateAsync(sequence, autoSave: false, cancellationToken: cancellationToken);

        return new FinanceInvoiceNumberAllocation
        {
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            InvoiceNumber = format(fiscalYear, sequenceNumber)
        };
    }
}
