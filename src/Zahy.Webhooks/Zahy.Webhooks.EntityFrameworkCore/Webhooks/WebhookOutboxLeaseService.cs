using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.Webhooks;

public class WebhookOutboxLeaseService : IWebhookOutboxLeaseService, ITransientDependency
{
    private readonly IDbContextProvider<ZahyWebhooksDbContext> _dbContextProvider;
    private readonly IDataFilter _dataFilter;

    public WebhookOutboxLeaseService(
        IDbContextProvider<ZahyWebhooksDbContext> dbContextProvider,
        IDataFilter dataFilter)
    {
        _dbContextProvider = dbContextProvider;
        _dataFilter = dataFilter;
    }

    public async Task<IReadOnlyList<Guid>> ClaimDueBatchAsync(
        int batchSize,
        DateTime now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var claimed = new List<Guid>();
        var leaseUntil = now.Add(leaseDuration);

        using (_dataFilter.Disable<IWebhookPartnerDataFilter>())
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();

            var candidateIds = await dbContext.WebhookOutboxMessages
                .Where(x =>
                    x.ScheduledAt <= now &&
                    (x.Status == WebhookOutboxStatus.Pending ||
                     (x.Status == WebhookOutboxStatus.Processing &&
                      x.LeaseExpiresAt != null &&
                      x.LeaseExpiresAt <= now)))
                .OrderBy(x => x.ScheduledAt)
                .Select(x => x.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            foreach (var id in candidateIds)
            {
                if (claimed.Count >= batchSize)
                {
                    break;
                }

                var updated = await dbContext.WebhookOutboxMessages
                    .Where(x =>
                        x.Id == id &&
                        x.ScheduledAt <= now &&
                        (x.Status == WebhookOutboxStatus.Pending ||
                         (x.Status == WebhookOutboxStatus.Processing &&
                          x.LeaseExpiresAt != null &&
                          x.LeaseExpiresAt <= now)))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(m => m.Status, WebhookOutboxStatus.Processing)
                            .SetProperty(m => m.LeaseExpiresAt, leaseUntil),
                        cancellationToken);

                if (updated == 1)
                {
                    claimed.Add(id);
                }
            }

            dbContext.ChangeTracker.Clear();
        }

        return claimed;
    }
}
