using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Zahy.Commission;

/// <summary>Mutable current user for commission ledger approval tests.</summary>
public sealed class CommissionTestCurrentUser : ICurrentUser, ISingletonDependency
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public bool IsAuthenticated => true;

    public Guid? Id => UserId;

    public string? UserName => "commission-test-user";

    public string? Name => UserName;

    public string? SurName => null;

    public string? Email => "commission-test@zahy.dev";

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
