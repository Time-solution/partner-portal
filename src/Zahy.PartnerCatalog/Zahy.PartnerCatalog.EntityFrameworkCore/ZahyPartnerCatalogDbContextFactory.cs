using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Design-time factory used ONLY by <c>dotnet ef migrations</c>.
/// Placeholder connection string — never applied via DbMigrator in Step 2a.
/// </summary>
public class ZahyPartnerCatalogDbContextFactory : IDesignTimeDbContextFactory<ZahyPartnerCatalogDbContext>
{
    private const string DesignTimePlaceholderConnectionString =
        "Server=(localdb)\\ZahyPartnerCatalogDesignTimeOnly;Database=ZahyPartnerCatalogDesignTimeOnly;Trusted_Connection=True;TrustServerCertificate=True";

    public ZahyPartnerCatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ZahyPartnerCatalogDbContext>()
            .UseSqlServer(DesignTimePlaceholderConnectionString)
            .Options;

        return new ZahyPartnerCatalogDbContext(options);
    }
}
