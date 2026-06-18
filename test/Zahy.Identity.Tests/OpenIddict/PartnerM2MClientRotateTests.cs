using System.Threading.Tasks;
using OpenIddict.Abstractions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OiPermissions = OpenIddict.Abstractions.OpenIddictConstants.Permissions;

namespace Zahy.Identity.OpenIddict;

public class PartnerM2MClientRotateTests : ZahyIdentityTestBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly IPartnerM2MClientProvisioner _provisioner;
    private readonly IOpenIddictApplicationManager _applicationManager;

    public PartnerM2MClientRotateTests()
    {
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _provisioner = GetRequiredService<IPartnerM2MClientProvisioner>();
        _applicationManager = GetRequiredService<IOpenIddictApplicationManager>();
    }

    [Fact]
    public async Task Should_Rotate_M2M_Client_Secret()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        var partnerId = Guid.NewGuid();
        PartnerM2MClientProvisionResult provisionResult = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            provisionResult = await _provisioner.ProvisionAsync(new PartnerM2MClientProvisionRequest
            {
                PartnerId = partnerId,
                DisplayName = "Acme Logistics LLC",
                Scopes = [ZahyScopes.OrdersRead]
            });
        });

        PartnerM2MClientRotateResult rotateResult = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            rotateResult = await _provisioner.RotateSecretAsync(provisionResult.ClientId);
        });

        rotateResult.ClientId.ShouldBe(provisionResult.ClientId);
        rotateResult.ClientSecret.ShouldNotBe(provisionResult.ClientSecret);

        await WithUnitOfWorkAsync(async () =>
        {
            var client = await _applicationManager.FindByClientIdAsync(provisionResult.ClientId);
            client.ShouldNotBeNull();
            (await _applicationManager.ValidateClientSecretAsync(client!, provisionResult.ClientSecret)).ShouldBeFalse();
            (await _applicationManager.ValidateClientSecretAsync(client!, rotateResult.ClientSecret)).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Should_Reject_Rotate_For_Unknown_Client()
    {
        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync());

        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _provisioner.RotateSecretAsync("missing-client");
            });
        });

        exception.Code.ShouldBe(ZahyIdentityErrorCodes.PartnerM2MClientNotFound);
    }
}
