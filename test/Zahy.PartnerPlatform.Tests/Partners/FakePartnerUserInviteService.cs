using System.Threading.Tasks;
using Zahy.Identity.Partners;

namespace Zahy.PartnerPlatform.Partners;

public class FakePartnerUserInviteService : IPartnerUserInviteService
{
    public PartnerUserInviteRequest? LastRequest { get; private set; }

    public Task<PartnerUserInviteResult> InviteAsync(PartnerUserInviteRequest request)
    {
        LastRequest = request;

        return Task.FromResult(new PartnerUserInviteResult
        {
            IdentityUserId = Guid.NewGuid(),
            Email = request.Email,
            SetPasswordToken = "test-set-password-token-once"
        });
    }

    public void Reset() => LastRequest = null;
}
