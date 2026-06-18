using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zahy.Identity.OpenIddict;

namespace Zahy.PartnerPlatform.Partners;

public class FakePartnerM2MClientProvisioner : IPartnerM2MClientProvisioner
{
    public PartnerM2MClientProvisionRequest? LastRequest { get; private set; }

    public Task<PartnerM2MClientProvisionResult> ProvisionAsync(PartnerM2MClientProvisionRequest request)
    {
        LastRequest = request;

        return Task.FromResult(new PartnerM2MClientProvisionResult
        {
            ClientId = PartnerM2MClientProvisioner.BuildClientId(request.PartnerId),
            ClientSecret = "test-client-secret-shown-once"
        });
    }

    public Task<PartnerM2MClientRotateResult> RotateSecretAsync(string clientId) =>
        Task.FromResult(new PartnerM2MClientRotateResult
        {
            ClientId = clientId,
            ClientSecret = "rotated-client-secret-once"
        });

    public void Reset() => LastRequest = null;
}
