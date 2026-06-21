using System;
using System.Threading;
using System.Threading.Tasks;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog;

public interface IPartnerCatalogPartnerTypeLookup
{
    Task<PartnerType?> GetPartnerTypeAsync(Guid partnerId, CancellationToken cancellationToken = default);
}
