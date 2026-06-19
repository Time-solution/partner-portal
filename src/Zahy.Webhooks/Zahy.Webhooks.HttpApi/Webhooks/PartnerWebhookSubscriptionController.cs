using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Webhooks;

[Route("api/partner/webhooks/subscriptions")]
public class PartnerWebhookSubscriptionController : AbpControllerBase
{
    private readonly IWebhookSubscriptionAppService _subscriptionAppService;

    public PartnerWebhookSubscriptionController(IWebhookSubscriptionAppService subscriptionAppService)
    {
        _subscriptionAppService = subscriptionAppService;
    }

    [HttpGet]
    public Task<System.Collections.Generic.List<WebhookSubscriptionDto>> GetListAsync() =>
        _subscriptionAppService.GetListAsync();

    [HttpGet("{id:guid}")]
    public Task<WebhookSubscriptionDto> GetAsync(Guid id) =>
        _subscriptionAppService.GetAsync(id);

    [HttpPost]
    public Task<WebhookSubscriptionCreateResultDto> CreateAsync([FromBody] CreateWebhookSubscriptionInput input) =>
        _subscriptionAppService.CreateAsync(input);

    [HttpPut("{id:guid}")]
    public Task<WebhookSubscriptionDto> UpdateAsync(Guid id, [FromBody] UpdateWebhookSubscriptionInput input) =>
        _subscriptionAppService.UpdateAsync(id, input);

    [HttpPost("{id:guid}/rotate-secret")]
    public Task<WebhookSecretRotateResultDto> RotateSecretAsync(Guid id) =>
        _subscriptionAppService.RotateSecretAsync(id);
}
