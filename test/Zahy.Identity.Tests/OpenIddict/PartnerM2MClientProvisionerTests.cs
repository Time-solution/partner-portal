using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OiPermissions = OpenIddict.Abstractions.OpenIddictConstants.Permissions;

namespace Zahy.Identity.OpenIddict;

public class PartnerM2MClientProvisionerTests : ZahyIdentityTestBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly IPartnerM2MClientProvisioner _provisioner;
    private readonly IOpenIddictApplicationManager _applicationManager;

    public PartnerM2MClientProvisionerTests()
    {
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _provisioner = GetRequiredService<IPartnerM2MClientProvisioner>();
        _applicationManager = GetRequiredService<IOpenIddictApplicationManager>();
    }

    [Fact]
    public async Task Should_Provision_Confidential_M2M_Client_With_Type_Scopes()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        var partnerId = Guid.NewGuid();
        PartnerM2MClientProvisionResult result = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            result = await _provisioner.ProvisionAsync(new PartnerM2MClientProvisionRequest
            {
                PartnerId = partnerId,
                DisplayName = "Acme Logistics LLC",
                Scopes = [ZahyScopes.OrdersRead, ZahyScopes.WebhooksManage]
            });
        });

        result.ClientId.ShouldBe(PartnerM2MClientProvisioner.BuildClientId(partnerId));
        result.ClientSecret.ShouldNotBeNullOrWhiteSpace();

        await WithUnitOfWorkAsync(async () =>
        {
            var client = await _applicationManager.FindByClientIdAsync(result.ClientId);
            client.ShouldNotBeNull();

            (await _applicationManager.GetClientTypeAsync(client!))
                .ShouldBe(ClientTypes.Confidential);

            (await _applicationManager.HasPermissionAsync(
                client!,
                OiPermissions.GrantTypes.ClientCredentials)).ShouldBeTrue();

            (await _applicationManager.HasPermissionAsync(
                client!,
                OiPermissions.Prefixes.Scope + ZahyScopes.OrdersRead)).ShouldBeTrue();

            (await _applicationManager.HasPermissionAsync(
                client!,
                OiPermissions.Prefixes.Scope + ZahyScopes.WebhooksManage)).ShouldBeTrue();

            var properties = await _applicationManager.GetPropertiesAsync(client!);
            properties[ZahyOpenIddictProperties.PartnerId].GetString().ShouldBe(partnerId.ToString("D"));

            (await _applicationManager.ValidateClientSecretAsync(client!, result.ClientSecret)).ShouldBeTrue();
            (await _applicationManager.ValidateClientSecretAsync(client!, "wrong-secret")).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Provision_For_Same_Partner()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        var partnerId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await _provisioner.ProvisionAsync(new PartnerM2MClientProvisionRequest
            {
                PartnerId = partnerId,
                DisplayName = "Acme Logistics LLC",
                Scopes = [ZahyScopes.OrdersRead]
            });
        });

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _provisioner.ProvisionAsync(new PartnerM2MClientProvisionRequest
                {
                    PartnerId = partnerId,
                    DisplayName = "Acme Logistics LLC",
                    Scopes = [ZahyScopes.OrdersRead]
                });
            });
        });

        exception.Code.ShouldBe(ZahyIdentityErrorCodes.PartnerM2MClientAlreadyExists);
    }
}
