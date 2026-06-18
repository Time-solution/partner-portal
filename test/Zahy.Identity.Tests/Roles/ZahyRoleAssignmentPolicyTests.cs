using System;
using Shouldly;
using Xunit;
using Zahy.Identity.Roles;

namespace Zahy.Identity.Roles;

public class ZahyRoleAssignmentPolicyTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid PartnerA = Guid.NewGuid();
    private static readonly Guid PartnerB = Guid.NewGuid();

    [Fact]
    public void Unknown_Role_Is_Rejected()
    {
        var result = ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PlatformSuperAdmin }, null, null,
            "Not.A.Role", null, null);

        result.ShouldBe(ZahyRoleAssignmentResult.UnknownRole);
    }

    [Fact]
    public void SuperAdmin_Can_Assign_Any_Role_Across_Boundaries()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PlatformSuperAdmin }, null, null,
            ZahyRoles.MerchantOwner, TenantB, null)
            .ShouldBe(ZahyRoleAssignmentResult.Allowed);

        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PlatformSuperAdmin }, null, null,
            ZahyRoles.PartnerOwner, null, PartnerB)
            .ShouldBe(ZahyRoleAssignmentResult.Allowed);
    }

    [Fact]
    public void PartnerOps_Can_Assign_Partner_Roles_Cross_Partner()
    {
        // Platform operator manages partners, so partner boundary does not bind it.
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PlatformPartnerOps }, null, null,
            ZahyRoles.PartnerManager, null, PartnerA)
            .ShouldBe(ZahyRoleAssignmentResult.Allowed);
    }

    [Fact]
    public void PartnerOps_Cannot_Assign_Platform_Roles()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PlatformPartnerOps }, null, null,
            ZahyRoles.PlatformFinance, null, null)
            .ShouldBe(ZahyRoleAssignmentResult.NotAssignable);
    }

    [Fact]
    public void MerchantOwner_Can_Assign_Lower_Roles_In_Same_Tenant()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.MerchantOwner }, TenantA, null,
            ZahyRoles.MerchantStaff, TenantA, null)
            .ShouldBe(ZahyRoleAssignmentResult.Allowed);
    }

    [Fact]
    public void MerchantOwner_Cannot_Cross_Tenant()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.MerchantOwner }, TenantA, null,
            ZahyRoles.MerchantStaff, TenantB, null)
            .ShouldBe(ZahyRoleAssignmentResult.TenantBoundary);
    }

    [Fact]
    public void MerchantManager_Cannot_Assign_Owner()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.MerchantManager }, TenantA, null,
            ZahyRoles.MerchantOwner, TenantA, null)
            .ShouldBe(ZahyRoleAssignmentResult.NotAssignable);
    }

    [Fact]
    public void PartnerOwner_Can_Assign_Within_Same_Partner()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PartnerOwner }, null, PartnerA,
            ZahyRoles.PartnerStaff, null, PartnerA)
            .ShouldBe(ZahyRoleAssignmentResult.Allowed);
    }

    [Fact]
    public void PartnerOwner_Cannot_Cross_Partner()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PartnerOwner }, null, PartnerA,
            ZahyRoles.PartnerStaff, null, PartnerB)
            .ShouldBe(ZahyRoleAssignmentResult.PartnerBoundary);
    }

    [Fact]
    public void Staff_Roles_Cannot_Assign_Anything()
    {
        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.MerchantStaff }, TenantA, null,
            ZahyRoles.MerchantViewer, TenantA, null)
            .ShouldBe(ZahyRoleAssignmentResult.NotAssignable);

        ZahyRoleAssignmentPolicy.Evaluate(
            new[] { ZahyRoles.PartnerStaff }, null, PartnerA,
            ZahyRoles.PartnerStaff, null, PartnerA)
            .ShouldBe(ZahyRoleAssignmentResult.NotAssignable);
    }
}
