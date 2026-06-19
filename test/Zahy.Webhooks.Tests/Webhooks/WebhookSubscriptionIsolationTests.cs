using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Security.Claims;
using Xunit;
using Zahy.Identity;
using Zahy.Identity.Roles;

namespace Zahy.Webhooks;

public class WebhookSubscriptionIsolationTests : ZahyWebhooksTestBase
{
    private readonly IRepository<WebhookSubscription, Guid> _subscriptionRepository;
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly TestCurrentPartner _currentPartner;

    public WebhookSubscriptionIsolationTests()
    {
        _subscriptionRepository = GetRequiredService<IRepository<WebhookSubscription, Guid>>();
        _subscriptionAppService = GetRequiredService<IWebhookSubscriptionAppService>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
    }

    [Fact]
    public async Task Should_Return_403_When_Partner_Accesses_Other_Partners_Subscription()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        var subscriptionId = _guidGenerator.Create();

        await WithUnitOfWorkAsync(async () =>
        {
            await _subscriptionRepository.InsertAsync(
                new WebhookSubscription(
                    subscriptionId,
                    partnerA,
                    "https://fake.local/a",
                    "secret-a",
                    [WebhookEventTypes.OrderCreated]),
                autoSave: true);
        });

        using (_principalAccessor.Change(CreatePartnerPrincipal(partnerB, ZahyRoles.PartnerOwner)))
        {
            _currentPartner.Id = partnerB;

            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _subscriptionAppService.GetAsync(subscriptionId);
            });
        }
    }

    private static ClaimsPrincipal CreatePartnerPrincipal(Guid partnerId, string role)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ZahyClaimTypes.PartnerId, partnerId.ToString("D")));
        identity.AddClaim(new Claim(AbpClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
