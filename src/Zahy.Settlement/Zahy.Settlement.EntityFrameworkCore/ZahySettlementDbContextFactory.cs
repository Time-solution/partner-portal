using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zahy.Settlement;

/// <summary>
/// Design-time factory used ONLY by <c>dotnet ef migrations</c> to build the model.
/// It uses a throwaway placeholder connection string that is NEVER connected to (migration
/// generation does not open a database) and deliberately does NOT read any real configuration
/// or the DbMigrator's connection string. Applying migrations is out of scope here.
/// </summary>
public class ZahySettlementDbContextFactory : IDesignTimeDbContextFactory<ZahySettlementDbContext>
{
    private const string DesignTimePlaceholderConnectionString =
        "Server=(localdb)\\ZahySettlementDesignTimeOnly;Database=ZahySettlementDesignTimeOnly;Trusted_Connection=True;TrustServerCertificate=True";

    public ZahySettlementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ZahySettlementDbContext>()
            .UseSqlServer(DesignTimePlaceholderConnectionString)
            .Options;

        return new ZahySettlementDbContext(options);
    }
}
