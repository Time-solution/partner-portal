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
}
