using System.Threading.Tasks;

namespace Zahy.Identity.OpenIddict;

/// <summary>
/// Anti-corruption boundary for provisioning partner machine-to-machine OpenIddict clients.
/// Implemented in Zahy.Identity; consumed by Zahy.PartnerPlatform on approval.
/// </summary>
public interface IPartnerM2MClientProvisioner
{
    Task<PartnerM2MClientProvisionResult> ProvisionAsync(PartnerM2MClientProvisionRequest request);

    Task<PartnerM2MClientRotateResult> RotateSecretAsync(string clientId);
}
