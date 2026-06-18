using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerPlatform.Partners;

[Route("api/partners")]
public class PartnerRegistrationController : AbpControllerBase
{
    private readonly IPartnerRegistrationAppService _registrationAppService;

    public PartnerRegistrationController(IPartnerRegistrationAppService registrationAppService)
    {
        _registrationAppService = registrationAppService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public Task<PartnerRegistrationResultDto> RegisterAsync([FromBody] PartnerRegistrationInput input) =>
        _registrationAppService.RegisterAsync(input);
}
