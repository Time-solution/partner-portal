using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Security.Claims;
using Xunit;
using Zahy.Identity;
using Zahy.Identity.Roles;

namespace Zahy.PartnerPlatform.Partners;

public class PartnerTeamIsolationTests : ZahyPartnerPlatformTestBase
{
    private readonly IRepository<PartnerUser, Guid> _partnerUserRepository;
    private readonly IPartnerTeamAppService _partnerTeamAppService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public PartnerTeamIsolationTests()
    {
        _partnerUserRepository = GetRequiredService<IRepository<PartnerUser, Guid>>();
        _partnerTeamAppService = GetRequiredService<IPartnerTeamAppService>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task Should_Return_403_When_Partner_User_Accesses_Other_Partners_Team_Member()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        var memberAId = _guidGenerator.Create();

        await WithUnitOfWorkAsync(async () =>
        {
            await _partnerUserRepository.InsertAsync(
                PartnerUser.CreateInvited(
                    memberAId,
                    partnerA,
                    Guid.NewGuid(),
                    ZahyRoles.PartnerStaff,
                    DateTime.UtcNow),
                autoSave: true);
        });

        using (_principalAccessor.Change(CreatePartnerPrincipal(partnerB, ZahyRoles.PartnerOwner)))
        {
            GetRequiredService<TestCurrentPartner>().Id = partnerB;

            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _partnerTeamAppService.GetMemberAsync(memberAId);
            });
        }
    }

    [Fact]
    public async Task Should_List_Only_Current_Partners_Team_Members()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            await _partnerUserRepository.InsertAsync(
                PartnerUser.CreateInvited(
                    _guidGenerator.Create(),
                    partnerA,
                    Guid.NewGuid(),
                    ZahyRoles.PartnerStaff,
                    DateTime.UtcNow),
                autoSave: true);

            await _partnerUserRepository.InsertAsync(
                PartnerUser.CreateInvited(
                    _guidGenerator.Create(),
                    partnerB,
                    Guid.NewGuid(),
                    ZahyRoles.PartnerStaff,
                    DateTime.UtcNow),
                autoSave: true);
        });

        using (_principalAccessor.Change(CreatePartnerPrincipal(partnerA, ZahyRoles.PartnerOwner)))
        {
            GetRequiredService<TestCurrentPartner>().Id = partnerA;

            var members = await _partnerTeamAppService.GetMembersAsync();
            members.Count.ShouldBe(1);
            members.Single().Role.ShouldBe(ZahyRoles.PartnerStaff);
        }
    }

    private static ClaimsPrincipal CreatePartnerPrincipal(Guid partnerId, string role)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ZahyClaimTypes.PartnerId, partnerId.ToString("D")));
        identity.AddClaim(new Claim(AbpClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }
}
