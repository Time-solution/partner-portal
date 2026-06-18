using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zahy.PartnerPlatform.EntityFrameworkCore;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public class ZahyPartnerPlatformDbContextFactory : IDesignTimeDbContextFactory<ZahyPartnerPlatformDbContext>
{
    public ZahyPartnerPlatformDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<ZahyPartnerPlatformDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new ZahyPartnerPlatformDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../../Zahy.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();
    }
}
