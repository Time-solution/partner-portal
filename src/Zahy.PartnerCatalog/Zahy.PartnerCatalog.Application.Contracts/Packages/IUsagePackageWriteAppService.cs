using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U2 — usage package authoring (partner self-publish + admin manages-all). CONFIG ONLY: create / edit /
/// publish / archive a pricing package. No billing is computed and no journal posted (that is U3).
/// </summary>
public interface IUsagePackageWriteAppService : IApplicationService
{
    Task<List<UsagePackageDto>> GetListAsync(UsagePackagesQuery query);

    Task<UsagePackageDto> CreateAsync(CreateUsagePackageInput input);

    Task<UsagePackageDto> UpdateAsync(Guid id, UpdateUsagePackageInput input);

    Task<UsagePackageDto> PublishAsync(Guid id);

    Task<UsagePackageDto> ArchiveAsync(Guid id);
}
