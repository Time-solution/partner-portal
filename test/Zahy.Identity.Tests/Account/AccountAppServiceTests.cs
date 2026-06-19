using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Xunit;
using Zahy.Identity.Account;
using Zahy.Identity.Auditing;
using Zahy.Identity.Roles;

namespace Zahy.Identity;

public class AccountAppServiceTests : ZahyIdentityTestBase
{
    private readonly IAccountAppService _accountAppService;
    private readonly IDataSeeder _dataSeeder;
    private readonly IdentityUserManager _userManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IRepository<AdminAuditLog, Guid> _auditRepository;

    public AccountAppServiceTests()
    {
        _accountAppService = GetRequiredService<IAccountAppService>();
        _dataSeeder = GetRequiredService<IDataSeeder>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _auditRepository = GetRequiredService<IRepository<AdminAuditLog, Guid>>();

        var accessor = GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = ServiceProvider
        };
    }

    [Fact]
    public async Task Should_Login_Seeded_Admin_And_Record_Success_Audit()
    {
        const string userName = "admin-login-audit";
        const string password = "1q2w3E*9";

        await WithUnitOfWorkAsync(() => _dataSeeder.SeedAsync(new DataSeedContext()));

        await WithUnitOfWorkAsync(async () =>
        {
            var user = new IdentityUser(
                _guidGenerator.Create(),
                userName,
                "admin-login-audit@zahy.dev",
                tenantId: null)
            {
                Name = "Platform SuperAdmin"
            };

            (await _userManager.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
            (await _userManager.AddToRoleAsync(user, ZahyRoles.PlatformSuperAdmin)).Succeeded.ShouldBeTrue();
        });

        LoginResultDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _accountAppService.LoginAsync(new LoginInput
            {
                UserName = userName,
                Password = password
            });
        });

        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.RequiresTwoFactor.ShouldBeFalse();

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _auditRepository.GetListAsync(x => x.Action == "Login");
            entries.Count.ShouldBe(1);
            entries.Single().Result.ShouldBe(AdminAuditResults.Success);
            entries.Single().TargetId.ShouldBe(userName);
        });
    }
}
