using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Zahy.Identity.Account;
using Zahy.Identity.Auditing;
using Zahy.Identity.Mfa;

namespace Zahy.Controllers;

/// <summary>
/// Owned first-party login: establishes the ABP identity cookie from a username
/// and password. The SPA then runs the OIDC Authorization Code + PKCE flow,
/// which completes silently because the cookie is already present. No dependency
/// on any third-party (makookapp) package.
/// </summary>
[Route("api/account")]
public class AccountController : AbpControllerBase
{
    private readonly AbpSignInManager _signInManager;
    private readonly IdentityUserManager _userManager;
    private readonly IAdminAuditLogger _auditLogger;
    private readonly IMfaStepUpPolicy _mfaStepUpPolicy;

    public AccountController(
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

    [HttpPost("login")]
    public async Task<LoginResultDto> LoginAsync(LoginInput input)
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
            // Step-up is wired but disabled by default (IAM-6); kept here so a
            // future enablement returns a clean signal to the client.
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

    [HttpPost("logout")]
    public async Task LogoutAsync()
    {
        if (CurrentUser.IsAuthenticated)
        {
            await _auditLogger.LogAsync("Logout", "User", CurrentUser.UserName);
        }

        await _signInManager.SignOutAsync();
    }
}
