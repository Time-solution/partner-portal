using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Settlement;

/// <summary>
/// Builds the bound posting result for each participation mode. Financial flows (Principal /
/// SubscriptionFee) are pure template computations — they move no money and persist no journal here
/// (posting persistence/disbursement is gated by <c>SettlementEngineOptions.PostingEnabled</c>,
/// default OFF, in a later phase). ReflectionOnly persists a non-posting <see cref="ReflectionLog"/>
/// row (dashboard/POS visibility), which is allowed regardless of the posting flag.
/// </summary>
public class SettlementPostingService : DomainService
{
    private readonly IRepository<ReflectionLog, Guid> _reflectionLogs;

    public SettlementPostingService(IRepository<ReflectionLog, Guid> reflectionLogs)
    {
        _reflectionLogs = reflectionLogs;
    }

    public PostingResult Principal(Money sellInclusive, Money buyInclusive, decimal vatRate) =>
        SettlementPostingTemplates.Principal(sellInclusive, buyInclusive, vatRate);

    public PostingResult SubscriptionFee(Money feeInclusive, decimal vatRate) =>
        SettlementPostingTemplates.SubscriptionFee(feeInclusive, vatRate);

    /// <summary>Records a reflection-only order and returns the (empty) non-posting result.</summary>
    [UnitOfWork]
    public virtual async Task<PostingResult> ReflectAsync(
        string orderReference,
        Money gross,
        Guid merchantId,
        DateTime occurredAt)
    {
        Check.NotNullOrWhiteSpace(orderReference, nameof(orderReference));
        Check.NotNull(gross, nameof(gross));

        await _reflectionLogs.InsertAsync(
            new ReflectionLog(GuidGenerator.Create(), orderReference, gross, merchantId, occurredAt),
            autoSave: true);

        return SettlementPostingTemplates.ReflectionOnly(gross.Currency);
    }
}
