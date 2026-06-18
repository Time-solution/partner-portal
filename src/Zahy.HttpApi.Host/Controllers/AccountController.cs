using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.Identity;
using Zahy.Identity.Account;

namespace Zahy.Controllers;

/// <summary>
/// HTTP adapter for owned first-party login (cookie + OIDC PKCE flow).
/// </summary>
[Route("api/account")]
public class AccountController : AbpControllerBase
{
    private readonly IAccountAppService _accountAppService;

    public AccountController(IAccountAppService accountAppService)
    {
        _accountAppService = accountAppService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public Task<LoginResultDto> LoginAsync([FromBody] LoginInput input) =>
        _accountAppService.LoginAsync(input);

    [HttpPost("logout")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public Task LogoutAsync() => _accountAppService.LogoutAsync();
}
