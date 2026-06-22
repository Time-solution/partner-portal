using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.Settlement.Read;

namespace Zahy.Settlement.Write;

/// <summary>
/// Write surface for operator-triggered settlement corrections. Today this is limited to triggering
/// an append-only SettlementCase reversal (compensating entry). Disbursement and commission ledger
/// reversals stay on their own dedicated paths.
/// </summary>
public interface ISettlementWriteAppService : IApplicationService
{
    /// <summary>
    /// Triggers an append-only reversal of the referenced settlement case. Idempotent on
    /// (OriginalSettlementCaseId, IdempotencyKey): replaying the same request returns the existing
    /// reversal instead of creating a second one.
    /// </summary>
    Task<SettlementCaseReadDto> TriggerCaseReversalAsync(TriggerReversalRequest request);
}
