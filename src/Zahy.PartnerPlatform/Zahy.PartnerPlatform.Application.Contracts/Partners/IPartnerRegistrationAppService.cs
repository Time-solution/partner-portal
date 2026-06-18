using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerPlatform.Partners;

public interface IPartnerRegistrationAppService : IApplicationService
{
    Task<PartnerRegistrationResultDto> RegisterAsync(PartnerRegistrationInput input);
}
