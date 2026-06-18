using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zahy.Identity.EntityFrameworkCore;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public class ZahyIdentityDbContextFactory : IDesignTimeDbContextFactory<ZahyIdentityDbContext>
{
    public ZahyIdentityDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<ZahyIdentityDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new ZahyIdentityDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Zahy.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true);

        return builder.Build();
    }
}
