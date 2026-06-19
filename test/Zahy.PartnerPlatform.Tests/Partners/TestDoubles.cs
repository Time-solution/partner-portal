using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Zahy.Identity;
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;

namespace Zahy.PartnerPlatform.Partners;

public class RecordingAdminAuditLogger : IAdminAuditLogger
{
    public List<RecordedAdminAuditEntry> Entries { get; } = [];

    public Task LogAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string result = AdminAuditResults.Success,
        string? extraData = null)
    {
        Entries.Add(new RecordedAdminAuditEntry(action, targetType, targetId, result, extraData));
        return Task.CompletedTask;
    }

    public void Clear() => Entries.Clear();
}

public sealed record RecordedAdminAuditEntry(
    string Action,
    string? TargetType,
    string? TargetId,
    string Result,
    string? ExtraData);

public class FailingPartnerM2MClientProvisioner : IPartnerM2MClientProvisioner
{
    public Task<PartnerM2MClientProvisionResult> ProvisionAsync(PartnerM2MClientProvisionRequest request) =>
        throw new BusinessException(ZahyIdentityErrorCodes.PartnerM2MClientAlreadyExists)
            .WithData("ClientId", PartnerM2MClientProvisioner.BuildClientId(request.PartnerId));

    public Task<PartnerM2MClientRotateResult> RotateSecretAsync(string clientId) =>
        throw new NotSupportedException();
}

public class DenyAllPermissionChecker : IPermissionChecker
{
    public Task<bool> IsGrantedAsync(string name) => Task.FromResult(false);

    public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) =>
        Task.FromResult(false);

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        var result = new MultiplePermissionGrantResult();
        foreach (var name in names)
        {
            result.Result[name] = PermissionGrantResult.Prohibited;
        }

        return Task.FromResult(result);
    }

    public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names) =>
        IsGrantedAsync(names);
}
