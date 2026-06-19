using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zahy.Commission;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public class ZahyCommissionDbContextFactory : IDesignTimeDbContextFactory<ZahyCommissionDbContext>
{
    public ZahyCommissionDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<ZahyCommissionDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new ZahyCommissionDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(ResolveDbMigratorPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();
    }

    private static string ResolveDbMigratorPath()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../Zahy.DbMigrator")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Zahy.DbMigrator"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(Path.Combine(path, "appsettings.json")))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Could not find Zahy.DbMigrator/appsettings.json for design-time migrations.");
    }
}
