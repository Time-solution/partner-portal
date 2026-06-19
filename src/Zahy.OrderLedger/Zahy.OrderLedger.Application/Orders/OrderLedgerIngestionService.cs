using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.OrderLedger;

public class OrderLedgerIngestionService : ApplicationService, IOrderLedgerIngestionService
{
    private readonly IRepository<OrderRecord, Guid> _orderRecordRepository;
    private readonly IOrderSource _orderSource;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IOrderLedgerWebhookNotifier _webhookNotifier;
    private readonly IOrderLedgerCommissionTrigger _commissionTrigger;

    public OrderLedgerIngestionService(
        IRepository<OrderRecord, Guid> orderRecordRepository,
        IOrderSource orderSource,
        IGuidGenerator guidGenerator,
        IOrderLedgerWebhookNotifier webhookNotifier,
        IOrderLedgerCommissionTrigger commissionTrigger)
    {
        _orderRecordRepository = orderRecordRepository;
        _orderSource = orderSource;
        _guidGenerator = guidGenerator;
        _webhookNotifier = webhookNotifier;
        _commissionTrigger = commissionTrigger;
    }

    [UnitOfWork]
    public virtual async Task<OrderIngestResult> IngestSnapshotAsync(
        OrderSourceSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindExistingAsync(snapshot);
        if (existing != null)
        {
            return ToResult(existing, isNew: false);
        }

        var previousStatus = await FindPreviousStatusAsync(snapshot);
        var record = OrderRecord.FromSnapshot(_guidGenerator.Create(), snapshot, Clock.Now);
        await _orderRecordRepository.InsertAsync(record, autoSave: true, cancellationToken: cancellationToken);
        await _webhookNotifier.NotifyIngestedAsync(record, previousStatus, cancellationToken);
        await _commissionTrigger.NotifyIngestedAsync(record, previousStatus, cancellationToken);
        return ToResult(record, isNew: true);
    }
    [UnitOfWork]
    public virtual async Task<OrderIngestBatchResult> PollAndIngestAsync(
        OrderSourcePollRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _orderSource.PollAsync(request, cancellationToken);
        var results = new List<OrderIngestResult>(snapshots.Count);
        var ingested = 0;
        var skipped = 0;

        foreach (var snapshot in snapshots)
        {
            var result = await IngestSnapshotAsync(snapshot, cancellationToken);
            results.Add(result);
            if (result.IsNew)
            {
                ingested++;
            }
            else
            {
                skipped++;
            }
        }

        return new OrderIngestBatchResult
        {
            PolledCount = snapshots.Count,
            IngestedCount = ingested,
            SkippedDuplicateCount = skipped,
            Results = results
        };
    }

    private async Task<OrderRecord?> FindExistingAsync(OrderSourceSnapshot snapshot)
    {
        var queryable = await _orderRecordRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x =>
            x.SourceSystem == snapshot.SourceSystem.Trim() &&
            x.SourceOrderId == snapshot.SourceOrderId.Trim() &&
            x.SourceVersion == snapshot.SourceVersion);
    }

    private async Task<OrderStatus?> FindPreviousStatusAsync(OrderSourceSnapshot snapshot)
    {
        var queryable = await _orderRecordRepository.GetQueryableAsync();
        var previous = queryable
            .Where(x =>
                x.SourceSystem == snapshot.SourceSystem.Trim() &&
                x.SourceOrderId == snapshot.SourceOrderId.Trim() &&
                x.SourceVersion < snapshot.SourceVersion)
            .OrderByDescending(x => x.SourceVersion)
            .FirstOrDefault();

        return previous?.Status;
    }
    private static OrderIngestResult ToResult(OrderRecord record, bool isNew) =>
        new()
        {
            OrderRecordId = record.Id,
            IsNew = isNew,
            SourceSystem = record.SourceSystem,
            SourceOrderId = record.SourceOrderId,
            SourceVersion = record.SourceVersion
        };
}
