using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerPlatform.Partners;

[Route("api/partner/credentials")]
public class PartnerCredentialsController : AbpControllerBase
{
    private readonly IPartnerCredentialsAppService _partnerCredentialsAppService;

    public PartnerCredentialsController(IPartnerCredentialsAppService partnerCredentialsAppService)
    {
        _partnerCredentialsAppService = partnerCredentialsAppService;
    }

    [HttpPost("m2m/rotate")]
    public Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync() =>
        _partnerCredentialsAppService.RotateM2MClientSecretAsync();
}
