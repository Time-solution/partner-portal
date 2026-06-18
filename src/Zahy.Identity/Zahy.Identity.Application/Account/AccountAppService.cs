using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Uow;
using Zahy.Identity.Account;
using Zahy.Identity.Auditing;
using Zahy.Identity.Mfa;

namespace Zahy.Identity;

public class AccountAppService : ApplicationService, IAccountAppService
{
    private readonly AbpSignInManager _signInManager;
    private readonly IdentityUserManager _userManager;
    private readonly IAdminAuditLogger _auditLogger;
    private readonly IMfaStepUpPolicy _mfaStepUpPolicy;

    public AccountAppService(
        AbpSignInManager signInManager,
        IdentityUserManager userManager,
        IAdminAuditLogger auditLogger,
        IMfaStepUpPolicy mfaStepUpPolicy)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogger = auditLogger;
        _mfaStepUpPolicy = mfaStepUpPolicy;
    }

    [UnitOfWork]
    public virtual async Task<LoginResultDto> LoginAsync(LoginInput input)
    {
        var user = await _userManager.FindByNameAsync(input.UserName)
                   ?? await _userManager.FindByEmailAsync(input.UserName);

        if (user == null)
        {
            await _auditLogger.LogAsync("Login", "User", input.UserName, AdminAuditResults.Denied);
            return new LoginResultDto { Success = false, Error = "InvalidCredentials" };
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName,
            input.Password,
            input.RememberMe,
            lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            await _auditLogger.LogAsync("Login", "User", user.UserName, AdminAuditResults.Denied, "RequiresTwoFactor");
            return new LoginResultDto { Success = false, RequiresTwoFactor = true };
        }

        if (result.IsLockedOut)
        {
            await _auditLogger.LogAsync("Login", "User", user.UserName, AdminAuditResults.Denied, "LockedOut");
            return new LoginResultDto { Success = false, Error = "LockedOut" };
        }

        if (!result.Succeeded)
        {
            await _auditLogger.LogAsync("Login", "User", user.UserName, AdminAuditResults.Denied);
            return new LoginResultDto { Success = false, Error = "InvalidCredentials" };
        }

        await _auditLogger.LogAsync("Login", "User", user.UserName, AdminAuditResults.Success);
        return new LoginResultDto { Success = true };
    }

    [UnitOfWork]
    public virtual async Task LogoutAsync()
    {
        if (CurrentUser.IsAuthenticated)
        {
            await _auditLogger.LogAsync("Logout", "User", CurrentUser.UserName);
        }

        await _signInManager.SignOutAsync();
    }
}
