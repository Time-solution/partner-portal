using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Zahy.Identity.Permissions;

namespace Zahy.Webhooks;

[Authorize(ZahyPermissions.Webhooks.Manage)]
public class WebhookSubscriptionAppService : ApplicationService, IWebhookSubscriptionAppService
{
    private readonly IRepository<WebhookSubscription, Guid> _subscriptionRepository;
    private readonly WebhookPartnerAccessGuard _accessGuard;
    private readonly IGuidGenerator _guidGenerator;

    public WebhookSubscriptionAppService(
        IRepository<WebhookSubscription, Guid> subscriptionRepository,
        WebhookPartnerAccessGuard accessGuard,
        IGuidGenerator guidGenerator)
    {
        _subscriptionRepository = subscriptionRepository;
        _accessGuard = accessGuard;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<List<WebhookSubscriptionDto>> GetListAsync()
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        var subscriptions = await _subscriptionRepository.GetListAsync(x => x.PartnerId == partnerId);
        return subscriptions.Select(WebhookDtoMapper.ToDto).ToList();
    }

    public virtual async Task<WebhookSubscriptionDto> GetAsync(Guid id)
    {
        var subscription = await GetSubscriptionOrThrowAsync(id);
        await _accessGuard.EnsureCanAccessPartnerAsync(subscription.PartnerId);
        return WebhookDtoMapper.ToDto(subscription);
    }

    [UnitOfWork]
    public virtual async Task<WebhookSubscriptionCreateResultDto> CreateAsync(CreateWebhookSubscriptionInput input)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        await EnsureUniqueTargetUrlAsync(partnerId, input.TargetUrl);

        var secret = WebhookSecretGenerator.Generate();
        var subscription = new WebhookSubscription(
            _guidGenerator.Create(),
            partnerId,
            input.TargetUrl,
            secret,
            input.EventTypes,
            WebhookDtoMapper.ToDomain(input.FilterRules));

        await _subscriptionRepository.InsertAsync(subscription, autoSave: true);

        return new WebhookSubscriptionCreateResultDto
        {
            Subscription = WebhookDtoMapper.ToDto(subscription),
            SigningSecret = secret
        };
    }

    [UnitOfWork]
    public virtual async Task<WebhookSubscriptionDto> UpdateAsync(Guid id, UpdateWebhookSubscriptionInput input)
    {
        var subscription = await GetSubscriptionOrThrowAsync(id);
        await _accessGuard.EnsureCanAccessPartnerAsync(subscription.PartnerId);

        if (!string.IsNullOrWhiteSpace(input.TargetUrl) &&
            !string.Equals(input.TargetUrl, subscription.TargetUrl, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureUniqueTargetUrlAsync(subscription.PartnerId, input.TargetUrl, subscription.Id);
            subscription.SetTargetUrl(input.TargetUrl);
        }

        if (input.EventTypes != null)
        {
            subscription.SetEventTypes(input.EventTypes);
        }

        if (input.FilterRules != null)
        {
            subscription.SetFilterRules(WebhookDtoMapper.ToDomain(input.FilterRules));
        }

        if (input.Status.HasValue)
        {
            switch (input.Status.Value)
            {
                case WebhookSubscriptionStatus.Active:
                    subscription.Resume();
                    break;
                case WebhookSubscriptionStatus.Paused:
                    subscription.Pause();
                    break;
                case WebhookSubscriptionStatus.Disabled:
                    subscription.Disable();
                    break;
            }
        }

        await _subscriptionRepository.UpdateAsync(subscription, autoSave: true);
        return WebhookDtoMapper.ToDto(subscription);
    }

    [UnitOfWork]
    public virtual async Task<WebhookSecretRotateResultDto> RotateSecretAsync(Guid id)
    {
        var subscription = await GetSubscriptionOrThrowAsync(id);
        await _accessGuard.EnsureCanAccessPartnerAsync(subscription.PartnerId);

        var secret = WebhookSecretGenerator.Generate();
        subscription.SetSigningSecret(secret);
        await _subscriptionRepository.UpdateAsync(subscription, autoSave: true);

        return new WebhookSecretRotateResultDto
        {
            SubscriptionId = subscription.Id,
            SigningSecret = secret
        };
    }

    private async Task<WebhookSubscription> GetSubscriptionOrThrowAsync(Guid id)
    {
        var subscription = await _subscriptionRepository.FindAsync(id);
        if (subscription == null)
        {
            throw new AbpAuthorizationException("Partner webhook access denied.");
        }

        return subscription;
    }

    private async Task EnsureUniqueTargetUrlAsync(Guid partnerId, string targetUrl, Guid? excludeId = null)
    {
        var queryable = await _subscriptionRepository.GetQueryableAsync();
        var normalized = targetUrl.Trim();
        var exists = queryable.Any(x =>
            x.PartnerId == partnerId &&
            x.Status != WebhookSubscriptionStatus.Disabled &&
            x.TargetUrl == normalized &&
            (!excludeId.HasValue || x.Id != excludeId.Value));

        if (exists)
        {
            throw new BusinessException(WebhookErrorCodes.DuplicateTargetUrl);
        }
    }
}
