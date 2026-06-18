using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace Zahy.Identity;

public class OpenIddictSeedTests : ZahyIdentityTestBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public OpenIddictSeedTests()
    {
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _applicationManager = GetRequiredService<IOpenIddictApplicationManager>();
        _scopeManager = GetRequiredService<IOpenIddictScopeManager>();
    }

    [Fact]
    public async Task Should_Seed_All_Scopes_And_Web_Clients()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            (await _scopeManager.CountAsync()).ShouldBe(ZahyScopes.All.Length);

            foreach (var scope in ZahyScopes.All)
            {
                (await _scopeManager.FindByNameAsync(scope.Name)).ShouldNotBeNull();
            }

            (await _applicationManager.FindByClientIdAsync("zahy-partner-web")).ShouldNotBeNull();
            (await _applicationManager.FindByClientIdAsync("zahy-admin-web")).ShouldNotBeNull();
            (await _applicationManager.FindByClientIdAsync("zahy-swagger")).ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Seeding_Should_Be_Idempotent()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            (await _scopeManager.CountAsync()).ShouldBe(ZahyScopes.All.Length);
            (await _applicationManager.CountAsync()).ShouldBe(3);
        });
    }

    [Fact]
    public async Task Partner_Web_Client_Should_Be_Public_With_AuthCode_And_Pkce()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        await WithUnitOfWorkAsync(async () =>
        {
            var client = await _applicationManager.FindByClientIdAsync("zahy-partner-web");
            client.ShouldNotBeNull();

            (await _applicationManager.GetClientTypeAsync(client!))
                .ShouldBe(OpenIddictConstants.ClientTypes.Public);

            (await _applicationManager.HasPermissionAsync(
                client!,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode)).ShouldBeTrue();

            (await _applicationManager.HasPermissionAsync(
                client!,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken)).ShouldBeTrue();

            (await _applicationManager.HasPermissionAsync(
                client!,
                OpenIddictConstants.Permissions.Prefixes.Scope + ZahyScopes.CatalogRead)).ShouldBeTrue();

            (await _applicationManager.HasRequirementAsync(
                client!,
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange)).ShouldBeTrue();
        });
    }
}
