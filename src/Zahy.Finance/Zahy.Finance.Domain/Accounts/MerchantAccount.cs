using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Finance;

public class MerchantAccount : AggregateRoot<Guid>, Volo.Abp.MultiTenancy.IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public FinanceAccountStatus Status { get; private set; }

    public Guid KycVerificationId { get; private set; }

    public DateTime OpenedAt { get; private set; }

    protected MerchantAccount()
    {
    }

    public static MerchantAccount Open(
        Guid id,
        Guid tenantId,
        Guid kycVerificationId,
        DateTime openedAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BusinessException(FinanceErrorCodes.InvalidPosting);
        }

        return new MerchantAccount
        {
            Id = id,
            TenantId = tenantId,
            KycVerificationId = kycVerificationId,
            Status = FinanceAccountStatus.Active,
            OpenedAt = openedAt
        };
    }

    public bool CanAcceptPostings => Status == FinanceAccountStatus.Active;

    public bool IsOperational => Status == FinanceAccountStatus.Active;
}
