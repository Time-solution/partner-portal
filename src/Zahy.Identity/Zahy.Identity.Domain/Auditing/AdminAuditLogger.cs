using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Zahy.Identity.Partners;

namespace Zahy.Identity.Auditing;

public class AdminAuditLogger : IAdminAuditLogger, ITransientDependency
{
    private readonly IRepository<AdminAuditLog, System.Guid> _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPartner _currentPartner;
    private readonly IGuidGenerator _guidGenerator;

    public AdminAuditLogger(
        IRepository<AdminAuditLog, System.Guid> repository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        ICurrentPartner currentPartner,
        IGuidGenerator guidGenerator)
    {
        _repository = repository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _currentPartner = currentPartner;
        _guidGenerator = guidGenerator;
    }

    public async Task LogAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string result = AdminAuditResults.Success,
        string? extraData = null)
    {
        var entry = new AdminAuditLog(
            _guidGenerator.Create(),
            actorUserId: _currentUser.Id,
            actorUserName: _currentUser.UserName ?? "(anonymous)",
            action: action,
            result: result,
            tenantId: _currentTenant.Id,
            partnerId: _currentPartner.Id,
            targetType: targetType,
            targetId: targetId,
            extraData: extraData);

        await _repository.InsertAsync(entry, autoSave: true);
    }
}
