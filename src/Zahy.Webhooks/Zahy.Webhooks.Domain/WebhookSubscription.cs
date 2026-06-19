using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Webhooks;

public class WebhookSubscription : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public string TargetUrl { get; private set; } = string.Empty;

    /// <summary>Signing secret stored for outbound HMAC; never exposed in DTOs except create/rotate.</summary>
    public string SigningSecret { get; private set; } = string.Empty;

    public string EventTypesJson { get; private set; } = "[]";

    public string? FilterRulesJson { get; private set; }

    public WebhookSubscriptionStatus Status { get; private set; }

    protected WebhookSubscription()
    {
    }

    public WebhookSubscription(
        Guid id,
        Guid partnerId,
        string targetUrl,
        string signingSecret,
        IEnumerable<string> eventTypes,
        WebhookFilterRules? filterRules = null)
    {
        Id = id;
        PartnerId = partnerId;
        SetTargetUrl(targetUrl);
        SetSigningSecret(signingSecret);
        SetEventTypes(eventTypes);
        SetFilterRules(filterRules);
        Status = WebhookSubscriptionStatus.Active;
    }

    public IReadOnlyList<string> GetEventTypes() =>
        WebhookSubscriptionJson.ParseStringList(EventTypesJson);

    public WebhookFilterRules? GetFilterRules() =>
        WebhookSubscriptionJson.ParseFilterRules(FilterRulesJson);

    public void SetTargetUrl(string targetUrl)
    {
        Check.NotNullOrWhiteSpace(targetUrl, nameof(targetUrl));
        if (targetUrl.Length > WebhookConsts.MaxTargetUrlLength)
        {
            throw new BusinessException(WebhookErrorCodes.InvalidTargetUrl);
        }

        TargetUrl = targetUrl.Trim();
    }

    public void SetSigningSecret(string signingSecret)
    {
        Check.NotNullOrWhiteSpace(signingSecret, nameof(signingSecret));
        if (signingSecret.Length > WebhookConsts.MaxSigningSecretLength)
        {
            throw new BusinessException(WebhookErrorCodes.InvalidTargetUrl);
        }

        SigningSecret = signingSecret;
    }

    public void SetEventTypes(IEnumerable<string> eventTypes)
    {
        var normalized = eventTypes.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
        if (normalized.Count == 0)
        {
            throw new BusinessException(WebhookErrorCodes.InvalidEventType);
        }

        foreach (var eventType in normalized)
        {
            if (!WebhookEventTypes.All.Contains(eventType))
            {
                throw new BusinessException(WebhookErrorCodes.InvalidEventType)
                    .WithData("EventType", eventType);
            }
        }

        EventTypesJson = WebhookSubscriptionJson.ToJson(normalized);
    }

    public void SetFilterRules(WebhookFilterRules? filterRules)
    {
        FilterRulesJson = filterRules == null ? null : WebhookSubscriptionJson.ToJson(filterRules);
    }

    public void Pause() => Status = WebhookSubscriptionStatus.Paused;

    public void Resume() => Status = WebhookSubscriptionStatus.Active;

    public void Disable() => Status = WebhookSubscriptionStatus.Disabled;

    public bool IsDeliverable => Status == WebhookSubscriptionStatus.Active;
}
