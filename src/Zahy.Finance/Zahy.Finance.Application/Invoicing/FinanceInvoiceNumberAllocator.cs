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

    public async Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default)
    {
        var fiscalYear = issueDate.Year;
        var queryable = await _sequenceRepository.GetQueryableAsync();
        var sequence = queryable.FirstOrDefault(x =>
            x.DocumentKind == FinanceDocumentKind.Invoice &&
            x.FiscalYear == fiscalYear);

        if (sequence == null)
        {
            sequence = InvoiceNumberSequence.Create(
                _guidGenerator.Create(),
                FinanceDocumentKind.Invoice,
                fiscalYear);
            await _sequenceRepository.InsertAsync(sequence, autoSave: false, cancellationToken: cancellationToken);
        }

        var sequenceNumber = sequence.AllocateNext();
        await _sequenceRepository.UpdateAsync(sequence, autoSave: false, cancellationToken: cancellationToken);

        return new FinanceInvoiceNumberAllocation
        {
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            InvoiceNumber = FinanceInvoiceNumberFormat.Format(fiscalYear, sequenceNumber)
        };
    }
}
