using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Zahy.Hosting;

/// <summary>
/// Wires ASP.NET DataProtection with a persisted key ring chosen from configuration.
/// Default (LocalFileSystem) keeps dev behaviour; Redis/EF are opt-in via config only.
/// </summary>
public static class ZahyDataProtectionConfigurator
{
    public static void Configure(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(DataProtectionKeyRingOptions.SectionName).Get<DataProtectionKeyRingOptions>()
                      ?? new DataProtectionKeyRingOptions();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(options.ApplicationName);

        switch (options.Provider)
        {
            case DataProtectionKeyRingProvider.Redis:
                ConfigureRedis(builder, options);
                break;

            case DataProtectionKeyRingProvider.EntityFrameworkCore:
                ConfigureEntityFrameworkCore(services, builder, configuration);
                break;

            case DataProtectionKeyRingProvider.LocalFileSystem:
            default:
                ConfigureLocalFileSystem(builder, options, environment);
                break;
        }
    }

    private static void ConfigureLocalFileSystem(
        IDataProtectionBuilder builder,
        DataProtectionKeyRingOptions options,
        IHostEnvironment environment)
    {
        var directory = options.Directory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(environment.ContentRootPath, "data-protection-keys");
        }

        Directory.CreateDirectory(directory);
        builder.PersistKeysToFileSystem(new DirectoryInfo(directory));
    }

    private static void ConfigureRedis(IDataProtectionBuilder builder, DataProtectionKeyRingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRing Provider is Redis but DataProtection:RedisConnectionString is empty.");
        }

        // Package: Microsoft.AspNetCore.DataProtection.StackExchangeRedis — only loaded when this provider is selected.
        builder.PersistKeysToStackExchangeRedis(
            StackExchange.Redis.ConnectionMultiplexer.Connect(options.RedisConnectionString),
            options.RedisKeyPrefix);
    }

    private static void ConfigureEntityFrameworkCore(
        IServiceCollection services,
        IDataProtectionBuilder builder,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyRing Provider is EntityFrameworkCore but ConnectionStrings:Default is empty.");
        }

        services.AddDbContext<ZahyDataProtectionKeyDbContext>(db =>
            db.UseSqlServer(connectionString));

        builder.PersistKeysToDbContext<ZahyDataProtectionKeyDbContext>();
    }
}

/// <summary>Minimal EF store for shared DataProtection keys (used only when Provider=EntityFrameworkCore).</summary>
public class ZahyDataProtectionKeyDbContext : DbContext, IDataProtectionKeyContext
{
    public ZahyDataProtectionKeyDbContext(DbContextOptions<ZahyDataProtectionKeyDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
}
