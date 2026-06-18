using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Zahy.Identity.Roles;

namespace Zahy.Identity.Seed;

/// <summary>
/// Dev-only test users (admin / merchant / partner) with a shared known password.
/// Runs only when <see cref="Environments.Development"/> AND <c>Zahy:DevSeed:Enabled=true</c>.
/// Never enable in Staging/Production.
/// </summary>
public class ZahyDevIdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>ABP template dev password — never used outside Development seed.</summary>
    private const string DevPassword = "1q2w3E*";

    /// <summary>Fixed tenant id for the dev merchant user (no Saas module required).</summary>
    private static readonly Guid DevMerchantTenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");

    private readonly IdentityUserManager _userManager;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;
    private readonly ZahyIdentityRoleDataSeedContributor _roleDataSeedContributor;

    public ZahyDevIdentityDataSeedContributor(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IGuidGenerator guidGenerator,
        IConfiguration configuration,
        ICurrentTenant currentTenant,
        ZahyIdentityRoleDataSeedContributor roleDataSeedContributor)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
        _currentTenant = currentTenant;
        _roleDataSeedContributor = roleDataSeedContributor;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevSeedAllowed())
        {
            return;
        }

        await EnsureDevUserAsync("admin", "admin@zahy.dev", tenantId: null, ZahyRoles.PlatformSuperAdmin, "Dev Platform SuperAdmin");

        await EnsureDevUserAsync("partner", "partner@zahy.dev", tenantId: null, ZahyRoles.PartnerOwner, "Dev Partner Owner");

        using (_currentTenant.Change(DevMerchantTenantId))
        {
            await _roleDataSeedContributor.SeedAsync(context);
            await EnsureDevUserAsync("merchant", "merchant@zahy.dev", DevMerchantTenantId, ZahyRoles.MerchantOwner, "Dev Merchant Owner");
        }
    }

    /// <summary>
    /// Guard: config flag (false in base appsettings) + ASPNETCORE/DOTNET environment must be Development.
    /// </summary>
    private bool IsDevSeedAllowed()
    {
        if (!_configuration.GetValue<bool>("Zahy:DevSeed:Enabled"))
        {
            return false;
        }

        var environment = _configuration["ASPNETCORE_ENVIRONMENT"]
                          ?? _configuration["DOTNET_ENVIRONMENT"]
                          ?? Environments.Production;

        return string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureDevUserAsync(
        string userName,
        string email,
        Guid? tenantId,
        string roleName,
        string displayName)
    {
        var normalizedName = _userManager.NormalizeName(userName);
        var existing = await _userRepository.FindByNormalizedUserNameAsync(normalizedName);

        if (existing != null)
        {
            await EnsureRoleAsync(existing, roleName);
            await ResetPasswordAsync(existing);
            return;
        }

        var user = new IdentityUser(_guidGenerator.Create(), userName, email, tenantId)
        {
            Name = displayName
        };

        (await _userManager.CreateAsync(user, DevPassword)).CheckErrors();
        (await _userManager.AddToRoleAsync(user, roleName)).CheckErrors();
    }

    private async Task EnsureRoleAsync(IdentityUser user, string roleName)
    {
        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            (await _userManager.AddToRoleAsync(user, roleName)).CheckErrors();
        }
    }

    private async Task ResetPasswordAsync(IdentityUser user)
    {
        if (await _userManager.IsLockedOutAsync(user))
        {
            (await _userManager.SetLockoutEndDateAsync(user, null)).CheckErrors();
        }

        if (await _userManager.CheckPasswordAsync(user, DevPassword))
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        (await _userManager.ResetPasswordAsync(user, token, DevPassword)).CheckErrors();
    }
}
