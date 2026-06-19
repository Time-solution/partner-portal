using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.Webhooks;

[Route("api/admin/webhooks")]
public class AdminWebhookController : AbpControllerBase
{
    private readonly IWebhookAdminAppService _adminAppService;

    public AdminWebhookController(IWebhookAdminAppService adminAppService)
    {
        _adminAppService = adminAppService;
    }

    [HttpGet("subscriptions")]
    public Task<List<WebhookSubscriptionDto>> GetSubscriptionsAsync([FromQuery] Guid? partnerId = null) =>
        _adminAppService.GetSubscriptionsAsync(partnerId);

    [HttpGet("deliveries")]
    public Task<PagedResultDto<WebhookDeliveryDto>> GetDeliveriesAsync(
        [FromQuery] PagedResultRequestDto input,
        [FromQuery] Guid? partnerId = null) =>
        _adminAppService.GetDeliveriesAsync(input, partnerId);

    [HttpGet("dead-letter")]
    public Task<PagedResultDto<WebhookDeadLetterDto>> GetDeadLettersAsync(
        [FromQuery] PagedResultRequestDto input,
        [FromQuery] Guid? partnerId = null) =>
        _adminAppService.GetDeadLettersAsync(input, partnerId);

    [HttpPost("dead-letter/{id:guid}/replay")]
    public Task ReplayDeadLetterAsync(Guid id) =>
        _adminAppService.ReplayDeadLetterAsync(id);
}
