using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Zahy.Identity.Auditing;

public class AdminAuditLoggerTests : ZahyIdentityTestBase
{
    private readonly IAdminAuditLogger _auditLogger;
    private readonly IRepository<AdminAuditLog, Guid> _repository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public AdminAuditLoggerTests()
    {
        _auditLogger = GetRequiredService<IAdminAuditLogger>();
        _repository = GetRequiredService<IRepository<AdminAuditLog, Guid>>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task Should_Record_A_Consequential_Action()
    {
        await WithUnitOfWorkAsync(() =>
            _auditLogger.LogAsync("RoleGranted", "User", "user-123"));

        await WithUnitOfWorkAsync(async () =>
        {
            var entries = await _repository.GetListAsync();
            entries.Count.ShouldBe(1);

            var entry = entries.Single();
            entry.Action.ShouldBe("RoleGranted");
            entry.TargetType.ShouldBe("User");
            entry.TargetId.ShouldBe("user-123");
            entry.Result.ShouldBe(AdminAuditResults.Success);
            entry.CreationTime.ShouldBeGreaterThan(default);
        });
    }

    [Fact]
    public async Task Should_Capture_The_Acting_Admin()
    {
        var actorId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, actorId.ToString()),
            new Claim(AbpClaimTypes.UserName, "ops.admin")
        }, "test"));

        using (_principalAccessor.Change(principal))
        {
            await WithUnitOfWorkAsync(() =>
                _auditLogger.LogAsync("PartnerSuspended", "Partner", "partner-9", AdminAuditResults.Denied));
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var entry = (await _repository.GetListAsync()).Single();
            entry.ActorUserId.ShouldBe(actorId);
            entry.ActorUserName.ShouldBe("ops.admin");
            entry.Result.ShouldBe(AdminAuditResults.Denied);
        });
    }
}
