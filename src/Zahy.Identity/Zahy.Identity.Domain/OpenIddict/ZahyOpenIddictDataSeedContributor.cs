using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OiPermissions = OpenIddict.Abstractions.OpenIddictConstants.Permissions;

namespace Zahy.Identity.OpenIddict;

/// <summary>
/// Idempotently seeds the OpenIddict scopes and the first-party SPA clients
/// (partner web, admin console, swagger). Partner machine-to-machine
/// (client-credentials) clients are provisioned later by partner onboarding.
/// </summary>
public class ZahyOpenIddictDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    public ZahyOpenIddictDataSeedContributor(
        IConfiguration configuration,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager)
    {
        _configuration = configuration;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        foreach (var scope in ZahyScopes.All)
        {
            if (await _scopeManager.FindByNameAsync(scope.Name) != null)
            {
                continue;
            }

            await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scope.Name,
                DisplayName = scope.DisplayName,
                Resources = { ZahyScopes.ApiResource }
            });
        }
    }

    private async Task CreateApplicationsAsync()
    {
        var partnerWeb = _configuration["OpenIddict:PartnerWebRootUrl"] ?? "http://localhost:5173";
        var adminWeb = _configuration["OpenIddict:AdminWebRootUrl"] ?? "http://localhost:5174";
        var swagger = _configuration["OpenIddict:SwaggerRootUrl"] ?? "https://localhost:44300";

        await CreatePublicSpaClientAsync("zahy-partner-web", "Zahy Partner Web", partnerWeb);
        await CreatePublicSpaClientAsync("zahy-admin-web", "Zahy Admin Console", adminWeb);
        await CreatePublicSpaClientAsync("zahy-swagger", "Zahy Swagger", swagger);
    }

    private async Task CreatePublicSpaClientAsync(string clientId, string displayName, string rootUrl)
    {
        if (await _applicationManager.FindByClientIdAsync(clientId) != null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = displayName,
            Permissions =
            {
                OiPermissions.Endpoints.Authorization,
                OiPermissions.Endpoints.Token,
                OiPermissions.Endpoints.EndSession,
                OiPermissions.Endpoints.Revocation,
                OiPermissions.Endpoints.Introspection,
                OiPermissions.GrantTypes.AuthorizationCode,
                OiPermissions.GrantTypes.RefreshToken,
                OiPermissions.ResponseTypes.Code,
                OiPermissions.Scopes.Profile,
                OiPermissions.Scopes.Email,
                OiPermissions.Scopes.Roles
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        };

        foreach (var scope in ZahyScopes.Names())
        {
            descriptor.Permissions.Add(OiPermissions.Prefixes.Scope + scope);
        }

        descriptor.RedirectUris.Add(new Uri($"{rootUrl.TrimEnd('/')}/auth/callback"));
        descriptor.PostLogoutRedirectUris.Add(new Uri(rootUrl.TrimEnd('/')));

        await _applicationManager.CreateAsync(descriptor);
    }
}
