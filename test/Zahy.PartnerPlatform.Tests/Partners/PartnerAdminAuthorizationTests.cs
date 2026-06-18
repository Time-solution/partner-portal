using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Xunit;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerPlatform.Partners;

[DependsOn(typeof(ZahyPartnerPlatformTestModule))]
public class ZahyPartnerPlatformAuthDeniedTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IPermissionChecker, DenyAllPermissionChecker>();
    }
}

public class PartnerAdminAuthorizationTests : AbpIntegratedTest<ZahyPartnerPlatformAuthDeniedTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public void PartnerAdminAppService_Should_Require_Partners_Manage_Permission()
    {
        var authorize = typeof(PartnerAdminAppService).GetCustomAttribute<AuthorizeAttribute>();
        authorize.ShouldNotBeNull();
        authorize!.Policy.ShouldBe(ZahyPermissions.Partners.Manage);
    }

    [Fact]
    public async Task Should_Deny_Partners_Manage_When_Permission_Checker_Denies()
    {
        var checker = GetRequiredService<IPermissionChecker>();
        (await checker.IsGrantedAsync(ZahyPermissions.Partners.Manage)).ShouldBeFalse();
    }
}
