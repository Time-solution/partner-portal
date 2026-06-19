using System;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class FinancePlatformSettings : AggregateRoot<Guid>
{
    public InvoiceGenerationMode DefaultInvoiceGenerationMode { get; private set; } =
        InvoiceGenerationMode.Manual;

    protected FinancePlatformSettings()
    {
    }

    public static FinancePlatformSettings CreateDefault(Guid id) =>
        new()
        {
            Id = id,
            DefaultInvoiceGenerationMode = InvoiceGenerationMode.Manual
        };

    public void SetDefaultInvoiceGenerationMode(InvoiceGenerationMode mode) =>
        DefaultInvoiceGenerationMode = mode;
}
