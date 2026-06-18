using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OiPermissions = OpenIddict.Abstractions.OpenIddictConstants.Permissions;

namespace Zahy.Identity.OpenIddict;

public class PartnerM2MClientProvisioner : DomainService, IPartnerM2MClientProvisioner
{
    private const string ClientIdPrefix = "zahy-partner-m2m-";

    private readonly IOpenIddictApplicationManager _applicationManager;

    public PartnerM2MClientProvisioner(IOpenIddictApplicationManager applicationManager)
    {
        _applicationManager = applicationManager;
    }

    public virtual async Task<PartnerM2MClientProvisionResult> ProvisionAsync(PartnerM2MClientProvisionRequest request)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        if (request.PartnerId == Guid.Empty)
        {
            throw new ArgumentException("PartnerId is required.", nameof(request));
        }

        if (request.Scopes == null || request.Scopes.Count == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(request));
        }

        var clientId = BuildClientId(request.PartnerId);

        if (await _applicationManager.FindByClientIdAsync(clientId) != null)
        {
            throw new BusinessException(ZahyIdentityErrorCodes.PartnerM2MClientAlreadyExists)
                .WithData("ClientId", clientId)
                .WithData("PartnerId", request.PartnerId);
        }

        var clientSecret = GenerateClientSecret();

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = ClientTypes.Confidential,
            DisplayName = request.DisplayName,
            Permissions =
            {
                OiPermissions.Endpoints.Token,
                OiPermissions.GrantTypes.ClientCredentials
            }
        };

        descriptor.Properties[ZahyOpenIddictProperties.PartnerId] =
            JsonSerializer.SerializeToElement(request.PartnerId.ToString("D"));

        foreach (var scope in request.Scopes)
        {
            descriptor.Permissions.Add(OiPermissions.Prefixes.Scope + scope);
        }

        await _applicationManager.CreateAsync(descriptor);

        return new PartnerM2MClientProvisionResult
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };
    }

    public virtual async Task<PartnerM2MClientRotateResult> RotateSecretAsync(string clientId)
    {
        Check.NotNullOrWhiteSpace(clientId, nameof(clientId));

        var application = await _applicationManager.FindByClientIdAsync(clientId);
        if (application == null)
        {
            throw new BusinessException(ZahyIdentityErrorCodes.PartnerM2MClientNotFound)
                .WithData("ClientId", clientId);
        }

        var newSecret = GenerateClientSecret();
        var descriptor = new OpenIddictApplicationDescriptor();
        await _applicationManager.PopulateAsync(descriptor, application);
        descriptor.ClientSecret = newSecret;
        await _applicationManager.UpdateAsync(application, descriptor);

        return new PartnerM2MClientRotateResult
        {
            ClientId = clientId,
            ClientSecret = newSecret
        };
    }

    public static string BuildClientId(Guid partnerId) => $"{ClientIdPrefix}{partnerId:D}";

    private static string GenerateClientSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
