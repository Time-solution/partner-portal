using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class PartnerFinancialAccount : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public FinanceAccountStatus Status { get; private set; }

    public Guid KycVerificationId { get; private set; }

    public DateTime OpenedAt { get; private set; }

    public InvoiceGenerationMode? InvoiceGenerationModeOverride { get; private set; }

    protected PartnerFinancialAccount()
    {
    }

    public static PartnerFinancialAccount Open(
        Guid id,
        Guid partnerId,
        Guid kycVerificationId,
        DateTime openedAt)
    {
        if (partnerId == Guid.Empty)
        {
            throw new BusinessException(FinanceErrorCodes.InvalidPosting);
        }

        return new PartnerFinancialAccount
        {
            Id = id,
            PartnerId = partnerId,
            KycVerificationId = kycVerificationId,
            Status = FinanceAccountStatus.Active,
            OpenedAt = openedAt
        };
    }

    public bool CanAcceptPostings => Status == FinanceAccountStatus.Active;

    public InvoiceGenerationMode ResolveInvoiceGenerationMode(InvoiceGenerationMode platformDefault) =>
        InvoiceGenerationModeOverride ?? platformDefault;

    public void SetInvoiceGenerationModeOverride(InvoiceGenerationMode? mode) =>
        InvoiceGenerationModeOverride = mode;

    public FinanceAccountStatus ApplyStatusTransition(FinanceAccountStatus targetStatus)
    {
        if (!FinanceAccountStatusRules.CanTransition(Status, targetStatus))
        {
            throw new BusinessException(FinanceErrorCodes.IllegalStatusTransition)
                .WithData("FromStatus", Status.ToString())
                .WithData("ToStatus", targetStatus.ToString());
        }

        Status = targetStatus;
        return Status;
    }
}
