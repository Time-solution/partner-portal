using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.Settlement.Read;

public interface ISettlementReadAppService : IApplicationService
{
    Task<List<SettlementCaseReadDto>> GetCasesAsync(SettlementPartnerQuery query);

    Task<List<SettlementCaseReadDto>> GetReversalsAsync(SettlementPartnerQuery query);

    Task<List<SettlementBillingChargeReadDto>> GetBillingChargesAsync(SettlementPartnerQuery query);
}
