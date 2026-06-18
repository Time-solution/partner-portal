using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerPlatform.Partners;

public interface IPartnerCredentialsAppService : IApplicationService
{
    Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync();
}
