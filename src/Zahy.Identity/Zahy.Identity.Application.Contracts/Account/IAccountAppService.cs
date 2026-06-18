using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.Identity.Account;

namespace Zahy.Identity;

public interface IAccountAppService : IApplicationService
{
    Task<LoginResultDto> LoginAsync(LoginInput input);
    Task LogoutAsync();
}
