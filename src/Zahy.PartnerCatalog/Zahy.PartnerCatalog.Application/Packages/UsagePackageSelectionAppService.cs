using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U4 — merchant package browse + selection. Browse returns a partner's PUBLISHED packages on the MERCHANT
/// view (sell/fee only; buy + margin structurally absent, reusing <see cref="UsagePackageVisibility"/>).
/// Select records an instant <see cref="UsagePackageSelection"/> (Active, no approval). SELECTION/LINK
/// ONLY — NOTHING is posted/persisted as money here: <c>SettlementEngineOptions.PostingEnabled</c> stays
/// OFF and U3 reads the active window for its compute-only billing.
/// </summary>
[Authorize]
public class UsagePackageSelectionAppService : ApplicationService, IUsagePackageSelectionAppService
{
    private readonly IRepository<UsagePackage, Guid> _packages;
    private readonly IRepository<UsagePackageSelection, Guid> _selections;

    public UsagePackageSelectionAppService(
        IRepository<UsagePackage, Guid> packages,
        IRepository<UsagePackageSelection, Guid> selections)
    {
        _packages = packages;
        _selections = selections;
    }

    public async Task<List<UsagePackageDto>> GetPublishedAsync(Guid partnerId)
    {
        var all = await _packages.GetListAsync();
        return all
            .Where(p => p.PartnerId == partnerId && p.Status == UsagePackageStatus.Published)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            // Merchant browse → SELL/fee only; buy + margin are absent (never widened).
            .Select(p => UsagePackageVisibility.ToDto(p, UsagePackageAudience.Merchant))
            .ToList();
    }

    public async Task<UsagePackageSelectionDto> SelectAsync(SelectUsagePackageInput input)
    {
        Check.NotNull(input, nameof(input));

        var package = await _packages.FindAsync(input.UsagePackageId);
        if (package == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.UsagePackageNotFound)
                .WithData("UsagePackageId", input.UsagePackageId);
        }

        // A merchant can only select a PUBLISHED package (drafts/archived are not browsable/selectable).
        if (package.Status != UsagePackageStatus.Published)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.UsagePackageNotPublished)
                .WithData("UsagePackageId", package.Id)
                .WithData("Status", package.Status.ToString());
        }

        var selection = new UsagePackageSelection(
            GuidGenerator.Create(),
            package.PartnerId,
            input.TenantId,
            package.Id,
            input.MerchantName,
            Clock.Now); // instant activation — no approval

        await _selections.InsertAsync(selection, autoSave: true);
        return Map(selection);
    }

    public async Task<UsagePackageSelectionDto> EndAsync(Guid id)
    {
        var selection = await _selections.FindAsync(id);
        if (selection == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.UsagePackageSelectionNotFound)
                .WithData("UsagePackageSelectionId", id);
        }

        selection.End(Clock.Now);
        await _selections.UpdateAsync(selection, autoSave: true);
        return Map(selection);
    }

    public async Task<List<UsagePackageSelectionDto>> GetForMerchantAsync(Guid tenantId)
    {
        var all = await _selections.GetListAsync();
        return all
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.ActivatedAt)
            .Select(Map)
            .ToList();
    }

    private static UsagePackageSelectionDto Map(UsagePackageSelection s) => new()
    {
        Id = s.Id,
        PartnerId = s.PartnerId,
        TenantId = s.TenantId,
        UsagePackageId = s.UsagePackageId,
        MerchantName = s.MerchantName,
        Status = s.Status,
        ActivatedAt = s.ActivatedAt,
        EndedAt = s.EndedAt,
    };
}
