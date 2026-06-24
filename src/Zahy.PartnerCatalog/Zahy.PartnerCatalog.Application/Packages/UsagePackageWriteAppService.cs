using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U2 — usage package authoring. Partner self-publishes their OWN packages; admin (manages-all) authors
/// any partner's. Reuses the catalog authoring-by-type + self-publish rules via
/// <see cref="UsagePackageWriteAccessGuard"/>. CONFIG ONLY — create/edit/publish/archive a pricing
/// package; NO billing math and NO journal posting happen here (that is the gated U3 phase).
/// </summary>
[Authorize]
public class UsagePackageWriteAppService : ApplicationService, IUsagePackageWriteAppService
{
    private readonly IRepository<UsagePackage, Guid> _repository;
    private readonly UsagePackageWriteAccessGuard _accessGuard;
    private readonly IPermissionChecker _permissionChecker;

    public UsagePackageWriteAppService(
        IRepository<UsagePackage, Guid> repository,
        UsagePackageWriteAccessGuard accessGuard,
        IPermissionChecker permissionChecker)
    {
        _repository = repository;
        _accessGuard = accessGuard;
        _permissionChecker = permissionChecker;
    }

    public async Task<List<UsagePackageDto>> GetListAsync(UsagePackagesQuery query)
    {
        var audience = await ResolveAudienceAsync();
        var all = await _repository.GetListAsync();
        return all
            .Where(p => query.PartnerId == null || p.PartnerId == query.PartnerId)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => UsagePackageVisibility.ToDto(p, audience))
            .ToList();
    }

    public async Task<UsagePackageDto> CreateAsync(CreateUsagePackageInput input)
    {
        Check.NotNull(input, nameof(input));
        await _accessGuard.EnsureCanAuthorAsync(input.PartnerId);

        var package = new UsagePackage(
            GuidGenerator.Create(),
            input.PartnerId,
            input.Name,
            input.UnitLabel,
            input.Mode,
            input.Currency,
            input.IncludedQuantity,
            input.BaseBuyAmount,
            input.BaseSellAmount,
            input.OverageBuyAmount,
            input.OverageSellAmount,
            input.Payer);

        package.SetTiers(MapTiers(input.Tiers));
        package.SetExplanation(input.PackageExplanation);

        await _repository.InsertAsync(package, autoSave: true);
        return UsagePackageVisibility.ToDto(package, await ResolveAudienceAsync());
    }

    public async Task<UsagePackageDto> UpdateAsync(Guid id, UpdateUsagePackageInput input)
    {
        Check.NotNull(input, nameof(input));
        var package = await RequireAsync(id);
        await _accessGuard.EnsureCanMutateAsync(package);

        package.SetDetails(
            input.Name,
            input.UnitLabel,
            input.IncludedQuantity,
            input.BaseBuyAmount,
            input.BaseSellAmount,
            input.OverageBuyAmount,
            input.OverageSellAmount,
            input.Payer);

        package.SetTiers(MapTiers(input.Tiers));
        package.SetExplanation(input.PackageExplanation);

        await _repository.UpdateAsync(package, autoSave: true);
        return UsagePackageVisibility.ToDto(package, await ResolveAudienceAsync());
    }

    private static IEnumerable<UsagePackageTier> MapTiers(IEnumerable<UsagePackageTierInput>? tiers) =>
        (tiers ?? Enumerable.Empty<UsagePackageTierInput>())
        .Select(t => new UsagePackageTier
        {
            FromQuantity = t.FromQuantity,
            ToQuantity = t.ToQuantity,
            BuyRate = t.BuyRate,
            SellRate = t.SellRate,
        });

    public async Task<UsagePackageDto> PublishAsync(Guid id)
    {
        var package = await RequireAsync(id);
        await _accessGuard.EnsureCanMutateAsync(package);

        package.Publish();
        await _repository.UpdateAsync(package, autoSave: true);
        return UsagePackageVisibility.ToDto(package, await ResolveAudienceAsync());
    }

    public async Task<UsagePackageDto> ArchiveAsync(Guid id)
    {
        var package = await RequireAsync(id);
        await _accessGuard.EnsureCanMutateAsync(package);

        package.Archive();
        await _repository.UpdateAsync(package, autoSave: true);
        return UsagePackageVisibility.ToDto(package, await ResolveAudienceAsync());
    }

    private async Task<UsagePackage> RequireAsync(Guid id)
    {
        var package = await _repository.FindAsync(id);
        if (package == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.UsagePackageNotFound)
                .WithData("UsagePackageId", id);
        }

        return package;
    }

    /// <summary>Admin (manages-all) sees full buy/sell/margin; a self-service partner sees their buy scope.</summary>
    private async Task<UsagePackageAudience> ResolveAudienceAsync() =>
        await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.AuthorManaged)
            ? UsagePackageAudience.Admin
            : UsagePackageAudience.Partner;
}
