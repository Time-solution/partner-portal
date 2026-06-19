using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class FinanceDocument : AggregateRoot<Guid>
{
    public FinanceAccountKind AccountKind { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid EntityId { get; private set; }

    public FinanceDocumentKind DocumentKind { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string InvoiceNumber { get; private set; } = string.Empty;

    public int FiscalYear { get; private set; }

    public int SequenceNumber { get; private set; }

    public decimal PostingSum { get; private set; }

    public string Currency { get; private set; } = FinanceConsts.DefaultCurrency;

    public DateTime GeneratedAt { get; private set; }

    public Guid? SourceRowId { get; private set; }

    protected FinanceDocument()
    {
    }

    public static FinanceDocument CreateInvoice(
        Guid id,
        FinanceAccountKind accountKind,
        Guid accountId,
        Guid entityId,
        string idempotencyKey,
        string invoiceNumber,
        int fiscalYear,
        int sequenceNumber,
        decimal postingSum,
        DateTime generatedAt,
        Guid? sourceRowId = null,
        string currency = FinanceConsts.DefaultCurrency)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));
        Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber));

        return new FinanceDocument
        {
            Id = id,
            AccountKind = accountKind,
            AccountId = accountId,
            EntityId = entityId,
            DocumentKind = FinanceDocumentKind.Invoice,
            IdempotencyKey = idempotencyKey.Trim(),
            InvoiceNumber = invoiceNumber.Trim(),
            FiscalYear = fiscalYear,
            SequenceNumber = sequenceNumber,
            PostingSum = FinanceMoney.RoundPosting(postingSum),
            Currency = currency,
            GeneratedAt = generatedAt,
            SourceRowId = sourceRowId
        };
    }
}
