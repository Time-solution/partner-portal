using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

public interface IFinanceAccountStatusService
{
    Task ChangePartnerStatusAsync(
        FinanceAccountStatusChangeRequest request,
        CancellationToken cancellationToken = default);

    Task ChangeMerchantStatusAsync(
        FinanceAccountStatusChangeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IMerchantOperationalStatusService
{
    Task<bool> IsOperationalAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public interface IFinanceInvoiceGenerationService
{
    Task<FinanceInvoiceGenerationResult> GeneratePartnerInvoiceOnDemandAsync(
        FinanceInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceInvoiceGenerationResult> TryGenerateForBillingChargeAsync(
        FinanceBillingInvoiceTriggerContext context,
        CancellationToken cancellationToken = default);

    Task<FinanceInvoiceGenerationResult> RegenerateInvoiceByIdempotencyAsync(
        FinanceAccountKind accountKind,
        Guid entityId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public interface IFinanceInvoiceNumberAllocator
{
    Task<FinanceInvoiceNumberAllocation> AllocateInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default);

    /// <summary>P4 — the MAN-yyyy-#### internal reference pool for manual invoices (own sequence,
    /// non-fiscal, gap-free under rollback). ZATCA numbering is Main-side and untouched.</summary>
    Task<FinanceInvoiceNumberAllocation> AllocateManualInvoiceNumberAsync(
        DateTime issueDate,
        CancellationToken cancellationToken = default);
}

public interface IInvoiceTrigger
{
    Task TryGenerateForBillingChargeAsync(
        FinanceBillingInvoiceTriggerContext context,
        CancellationToken cancellationToken = default);
}

public sealed class FinanceAccountStatusChangeRequest
{
    public Guid EntityId { get; init; }

    public FinanceAccountStatus TargetStatus { get; init; }

    public string? Reason { get; init; }
}

public sealed class FinanceBillingInvoiceTriggerContext
{
    public Guid BillingChargeId { get; init; }

    public bool IsNew { get; init; }

    public Guid PartnerId { get; init; }

    public Guid? TenantId { get; init; }

    public FinanceAccountKind AccountKind { get; init; }
}

public sealed class FinanceInvoiceGenerationResult
{
    public Guid DocumentId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public byte[] Content { get; init; } = Array.Empty<byte>();

    public decimal PostingSum { get; init; }

    public bool IsNew { get; init; }

    public string ContentType { get; init; } = "application/pdf";

    public string FileName { get; init; } = string.Empty;
}
