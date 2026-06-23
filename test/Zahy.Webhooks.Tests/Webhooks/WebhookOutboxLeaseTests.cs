using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookOutboxLeaseTests : ZahyWebhooksTestBase
{
    private readonly IWebhookOutboxPublisher _publisher;
    private readonly IWebhookOutboxLeaseService _leaseService;
    private readonly IRepository<WebhookOutboxMessage, Guid> _outboxRepository;
    private readonly IClock _clock;

    public WebhookOutboxLeaseTests()
    {
        _publisher = GetRequiredService<IWebhookOutboxPublisher>();
        _leaseService = GetRequiredService<IWebhookOutboxLeaseService>();
        _outboxRepository = GetRequiredService<IRepository<WebhookOutboxMessage, Guid>>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task Active_lease_prevents_second_instance_from_claiming_same_row()
    {
        var partnerId = Guid.NewGuid();
        Guid outboxId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            outboxId = await _publisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderCreated,
                IdempotencyKey = "order.created:lease-test:1",
                PayloadJson = """{"orderId":"lease-test"}"""
            });
        });

        IReadOnlyList<Guid> firstClaim = Array.Empty<Guid>();
        IReadOnlyList<Guid> secondClaim = Array.Empty<Guid>();
        DateTime claimTime = default;

        await WithUnitOfWorkAsync(async () =>
        {
            claimTime = _clock.Now;
            firstClaim = await _leaseService.ClaimDueBatchAsync(
                batchSize: 10,
                claimTime,
                leaseDuration: TimeSpan.FromMinutes(5));
        });

        await WithUnitOfWorkAsync(async () =>
        {
            secondClaim = await _leaseService.ClaimDueBatchAsync(
                batchSize: 10,
                _clock.Now,
                leaseDuration: TimeSpan.FromMinutes(5));
        });

        firstClaim.Count.ShouldBe(1);
        firstClaim[0].ShouldBe(outboxId);
        secondClaim.ShouldBeEmpty();

        await WithUnitOfWorkAsync(async () =>
        {
            var stored = await _outboxRepository.GetAsync(outboxId);
            stored.Status.ShouldBe(WebhookOutboxStatus.Processing);
            stored.LeaseExpiresAt.ShouldNotBeNull();
            stored.LeaseExpiresAt!.Value.ShouldBeGreaterThan(claimTime);
        });
    }

    [Fact]
    public async Task Expired_lease_allows_reclaim_of_stale_processing_row()
    {
        var partnerId = Guid.NewGuid();
        Guid outboxId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            outboxId = await _publisher.EnqueueAsync(new WebhookEnqueueRequest
            {
                PartnerId = partnerId,
                EventType = WebhookEventTypes.OrderCreated,
                IdempotencyKey = "order.created:lease-reclaim:1",
                PayloadJson = """{"orderId":"lease-reclaim"}"""
            });
        });

        DateTime firstClaimTime = default;

        await WithUnitOfWorkAsync(async () =>
        {
            firstClaimTime = _clock.Now;
            var first = await _leaseService.ClaimDueBatchAsync(10, firstClaimTime, TimeSpan.FromMinutes(5));
            first.Count.ShouldBe(1);
            first[0].ShouldBe(outboxId);
        });

        var afterLeaseExpiry = firstClaimTime.AddMinutes(6);

        await WithUnitOfWorkAsync(async () =>
        {
            var reclaimed = await _leaseService.ClaimDueBatchAsync(10, afterLeaseExpiry, TimeSpan.FromMinutes(5));
            reclaimed.Count.ShouldBe(1);
            reclaimed[0].ShouldBe(outboxId);

            var stored = await _outboxRepository.GetAsync(outboxId);
            stored.LeaseExpiresAt!.Value.ShouldBeGreaterThan(afterLeaseExpiry);
        });
    }
}
