using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Authorizations;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.OpenIddict.Tokens;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Auditing;

namespace Zahy.Identity;

/// <summary>
/// The single DbContext owned by the Zahy.Identity module. It hosts the ABP
/// Identity, OpenIddict and PermissionManagement tables (via ReplaceDbContext)
/// so the module owns one schema and one migration set on the shared database.
/// </summary>
[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(IOpenIddictDbContext))]
[ReplaceDbContext(typeof(IPermissionManagementDbContext))]
[ConnectionStringName("Default")]
public class ZahyIdentityDbContext :
    AbpDbContext<ZahyIdentityDbContext>,
    IIdentityDbContext,
    IOpenIddictDbContext,
    IPermissionManagementDbContext
{
    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // OpenIddict
    public DbSet<OpenIddictApplication> Applications { get; set; }
    public DbSet<OpenIddictAuthorization> Authorizations { get; set; }
    public DbSet<OpenIddictScope> Scopes { get; set; }
    public DbSet<OpenIddictToken> Tokens { get; set; }

    // Permission Management
    public DbSet<PermissionGroupDefinitionRecord> PermissionGroups { get; set; }
    public DbSet<PermissionDefinitionRecord> Permissions { get; set; }
    public DbSet<PermissionGrant> PermissionGrants { get; set; }
    public DbSet<ResourcePermissionGrant> ResourcePermissionGrants { get; set; }

    // Zahy.Identity own aggregates
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    public ZahyIdentityDbContext(DbContextOptions<ZahyIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigurePermissionManagement();

        builder.Entity<AdminAuditLog>(b =>
        {
            b.ToTable("ZahyAdminAuditLogs");
            b.ConfigureByConvention();
            b.Property(x => x.ActorUserName).HasMaxLength(256);
            b.Property(x => x.Action).IsRequired().HasMaxLength(128);
            b.Property(x => x.TargetType).HasMaxLength(128);
            b.Property(x => x.TargetId).HasMaxLength(256);
            b.Property(x => x.Result).IsRequired().HasMaxLength(64);
            b.Property(x => x.ExtraData).HasMaxLength(2048);
            b.HasIndex(x => new { x.TenantId, x.CreationTime });
        });
    }
}
