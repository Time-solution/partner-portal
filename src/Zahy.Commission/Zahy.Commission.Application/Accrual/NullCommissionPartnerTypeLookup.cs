using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public class NullCommissionPartnerTypeLookup : ICommissionPartnerTypeLookup, ITransientDependency
{
    public Task<PartnerType?> GetPartnerTypeAsync(Guid partnerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PartnerType?>(null);
}
