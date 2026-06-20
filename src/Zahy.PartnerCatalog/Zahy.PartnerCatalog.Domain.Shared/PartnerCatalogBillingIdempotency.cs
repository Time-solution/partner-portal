using System;
using Volo.Abp;

namespace Zahy.PartnerCatalog;

public static class PartnerCatalogBillingIdempotency
{
    public static string BuildMerchantSubscriptionPeriodKey(
        Guid tenantId,
        Guid merchantActivationId,
        string billingPeriodKey)
    {
        if (tenantId == Guid.Empty || merchantActivationId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidSubscriptionFee)
                .WithData("Reason", "TenantAndActivationRequired");
        }

        if (string.IsNullOrWhiteSpace(billingPeriodKey))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidSubscriptionFee)
                .WithData("Reason", "BillingPeriodKeyRequired");
        }

        return $"pcat:sub:{tenantId:N}:{merchantActivationId:N}:{billingPeriodKey.Trim().ToLowerInvariant()}";
    }

    public static string NormalizeBillingPeriodKey(string billingPeriodKey) =>
        billingPeriodKey.Trim().ToLowerInvariant();
}
