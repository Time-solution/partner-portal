using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Commission;

public class BillingChargeValidationTests : ZahyCommissionTestBase
{
    private readonly IBillingChargeService _billingChargeService;

    public BillingChargeValidationTests()
    {
        _billingChargeService = GetRequiredService<IBillingChargeService>();
    }

    [Fact]
    public async Task Zero_Amount_Billing_Charge_Is_Rejected()
    {
        var partnerId = Guid.NewGuid();

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = partnerId,
                    ChargeTarget = BillingChargeTarget.Partner,
                    Kind = BillingChargeKind.Subscription,
                    Amount = 0m,
                    IdempotencyKey = "billing:zero-amount-test"
                });
            });
        });

        exception.Code.ShouldBe(CommissionErrorCodes.InvalidBillingCharge);
    }
}
