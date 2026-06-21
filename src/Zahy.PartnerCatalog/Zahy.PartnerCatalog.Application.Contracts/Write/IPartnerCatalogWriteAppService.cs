using System;
using System.Threading.Tasks;
using Zahy.PartnerCatalog.Read;

namespace Zahy.PartnerCatalog.Write;

public interface IPartnerCatalogWriteAppService
{
    Task<PartnerCatalogItemReadDto> CreateAsync(CreatePartnerCatalogItemInput input);

    Task<PartnerCatalogItemReadDto> UpdateAsync(Guid id, UpdatePartnerCatalogItemInput input);

    Task<PartnerCatalogItemReadDto> PublishAsync(Guid id);

    Task<PartnerCatalogItemReadDto> ArchiveAsync(Guid id);
}
