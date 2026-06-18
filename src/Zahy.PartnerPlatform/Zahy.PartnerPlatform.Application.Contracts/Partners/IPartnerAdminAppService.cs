using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerPlatform.Partners;

public interface IPartnerAdminAppService : IApplicationService
{
    Task<PagedResultDto<PartnerListItemDto>> GetListAsync(GetPartnersInput input);

    Task<PartnerDto> GetAsync(Guid id);

    Task<PartnerApproveResultDto> ApproveAsync(Guid id, PartnerLifecycleActionInput? input = null);

    Task<PartnerM2MRotateResultDto> RotateM2MClientSecretAsync(Guid id);

    Task<PartnerDto> RejectAsync(Guid id, PartnerLifecycleActionInput? input = null);

    Task<PartnerDto> SuspendAsync(Guid id, PartnerLifecycleActionInput? input = null);

    Task<PartnerDto> ReactivateAsync(Guid id, PartnerLifecycleActionInput? input = null);

    Task<PartnerDto> CloseAsync(Guid id, PartnerLifecycleActionInput? input = null);
}
