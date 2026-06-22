using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;
using Zahy.Identity.Auditing;

namespace Zahy.Settlement;

/// <summary>Captures admin audit entries in-memory so write tests can assert the trail.</summary>
public sealed class RecordingSettlementAuditLogger : IAdminAuditLogger
{
    public List<RecordedSettlementAuditEntry> Entries { get; } = new();

    public Task LogAsync(
        string action,
        string? targetType = null,
        string? targetId = null,
        string result = AdminAuditResults.Success,
        string? extraData = null)
    {
        Entries.Add(new RecordedSettlementAuditEntry(action, targetType, targetId, result, extraData));
        return Task.CompletedTask;
    }
}

public sealed record RecordedSettlementAuditEntry(
    string Action,
    string? TargetType,
    string? TargetId,
    string Result,
    string? ExtraData);

/// <summary>Mutable current user for settlement write tests (actor of a reversal).</summary>
public sealed class SettlementTestCurrentUser : ICurrentUser, ISingletonDependency
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public bool IsAuthenticated => true;

    public Guid? Id => UserId;

    public string? UserName => "settlement-test-user";

    public string? Name => UserName;

    public string? SurName => null;

    public string? Email => "settlement-test@zahy.dev";

    public bool EmailVerified => true;

    public string? PhoneNumber => null;

    public bool PhoneNumberVerified => false;

    public Guid? TenantId => null;

    public string[] Roles => Array.Empty<string>();

    public Claim[] FindClaims(string claimType) => Array.Empty<Claim>();

    public Claim[] GetAllClaims() => Array.Empty<Claim>();

    public Claim? FindClaim(string claimType) => null;

    public T? FindClaimValue<T>(string claimType) where T : class => null;

    public bool IsInRole(string roleName) => false;
}
