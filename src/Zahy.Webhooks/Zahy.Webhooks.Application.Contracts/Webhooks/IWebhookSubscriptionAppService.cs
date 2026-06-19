using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Zahy.Webhooks;

public class WebhookFilterRulesDto
{
    public List<Guid>? TenantIds { get; set; }

    public List<string>? Directions { get; set; }

    public decimal? MinAmountSar { get; set; }
}

public class CreateWebhookSubscriptionInput
{
    public string TargetUrl { get; set; } = string.Empty;

    public List<string> EventTypes { get; set; } = [];

    public WebhookFilterRulesDto? FilterRules { get; set; }
}

public class UpdateWebhookSubscriptionInput
{
    public string? TargetUrl { get; set; }

    public List<string>? EventTypes { get; set; }

    public WebhookFilterRulesDto? FilterRules { get; set; }

    public WebhookSubscriptionStatus? Status { get; set; }
}

public class WebhookSubscriptionDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }

    public string TargetUrl { get; set; } = string.Empty;

    public List<string> EventTypes { get; set; } = [];

    public WebhookFilterRulesDto? FilterRules { get; set; }

    public WebhookSubscriptionStatus Status { get; set; }

    public DateTime CreationTime { get; set; }
}

public class WebhookSubscriptionCreateResultDto
{
    public WebhookSubscriptionDto Subscription { get; set; } = new();

    /// <summary>Plaintext signing secret — returned once, never stored in logs.</summary>
    public string SigningSecret { get; set; } = string.Empty;
}

public class WebhookSecretRotateResultDto
{
    public Guid SubscriptionId { get; set; }

    public string SigningSecret { get; set; } = string.Empty;
}

public class WebhookDeliveryDto : EntityDto<Guid>
{
    public Guid OutboxMessageId { get; set; }

    public Guid SubscriptionId { get; set; }

    public Guid PartnerId { get; set; }

    public int AttemptNumber { get; set; }

    public WebhookDeliveryAttemptStatus Status { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? ResponseBodySnippet { get; set; }

    public int DurationMs { get; set; }

    public string? ErrorCode { get; set; }

    public DateTime AttemptedAt { get; set; }
}

public class WebhookDeadLetterDto : EntityDto<Guid>
{
    public Guid OutboxMessageId { get; set; }

    public Guid PartnerId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTime FinalAttemptAt { get; set; }

    public DateTime? ReplayedAt { get; set; }
}

public interface IWebhookSubscriptionAppService : IApplicationService
{
    Task<List<WebhookSubscriptionDto>> GetListAsync();

    Task<WebhookSubscriptionDto> GetAsync(Guid id);

    Task<WebhookSubscriptionCreateResultDto> CreateAsync(CreateWebhookSubscriptionInput input);

    Task<WebhookSubscriptionDto> UpdateAsync(Guid id, UpdateWebhookSubscriptionInput input);

    Task<WebhookSecretRotateResultDto> RotateSecretAsync(Guid id);
}

public interface IWebhookAdminAppService : IApplicationService
{
    Task<List<WebhookSubscriptionDto>> GetSubscriptionsAsync(Guid? partnerId = null);

    Task<PagedResultDto<WebhookDeliveryDto>> GetDeliveriesAsync(PagedResultRequestDto input, Guid? partnerId = null);

    Task<PagedResultDto<WebhookDeadLetterDto>> GetDeadLettersAsync(PagedResultRequestDto input, Guid? partnerId = null);

    Task ReplayDeadLetterAsync(Guid deadLetterId);
}
