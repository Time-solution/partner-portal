using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Xunit;
using Zahy.Identity;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerAccessGuardTests : ZahyPartnerPlatformTestBase
{
    private readonly PartnerAccessGuard _accessGuard;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public PartnerAccessGuardTests()
    {
        _accessGuard = GetRequiredService<PartnerAccessGuard>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task Should_Deny_Cross_Partner_Access()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();

        using (_principalAccessor.Change(CreatePartnerPrincipal(partnerB)))
        {
            GetRequiredService<TestCurrentPartner>().Id = partnerB;

            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _accessGuard.EnsureCanAccessPartnerAsync(partnerA);
            });
        }
    }

    [Fact]
    public async Task Should_Allow_Access_To_Own_Partner()
    {
        var partnerId = Guid.NewGuid();

        using (_principalAccessor.Change(CreatePartnerPrincipal(partnerId)))
        {
            GetRequiredService<TestCurrentPartner>().Id = partnerId;
            await _accessGuard.EnsureCanAccessPartnerAsync(partnerId);
        }
    }

    private static ClaimsPrincipal CreatePartnerPrincipal(Guid partnerId, string? role = null)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ZahyClaimTypes.PartnerId, partnerId.ToString("D")));
        if (role != null)
        {
            identity.AddClaim(new Claim(AbpClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(identity);
    }
}
