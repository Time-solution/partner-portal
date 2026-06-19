using System;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class InvoiceNumberSequence : AggregateRoot<Guid>
{
    public FinanceDocumentKind DocumentKind { get; private set; }

    public int FiscalYear { get; private set; }

    public int LastNumber { get; private set; }

    protected InvoiceNumberSequence()
    {
    }

    public static InvoiceNumberSequence Create(
        Guid id,
        FinanceDocumentKind documentKind,
        int fiscalYear) =>
        new()
        {
            Id = id,
            DocumentKind = documentKind,
            FiscalYear = fiscalYear,
            LastNumber = 0
        };

    public int AllocateNext() => ++LastNumber;
}
