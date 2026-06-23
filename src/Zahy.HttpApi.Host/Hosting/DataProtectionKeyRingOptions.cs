namespace Zahy.Hosting;

/// <summary>
/// Configurable DataProtection key-ring persistence (LB-ready seam — no live Redis required unless configured).
/// </summary>
public sealed class DataProtectionKeyRingOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>Shared application name — must match on every instance sharing a key ring.</summary>
    public string ApplicationName { get; set; } = "Zahy.PartnerPlatform";

    public DataProtectionKeyRingProvider Provider { get; set; } = DataProtectionKeyRingProvider.LocalFileSystem;

    /// <summary>Directory for <see cref="DataProtectionKeyRingProvider.LocalFileSystem"/> (shared mount in prod).</summary>
    public string? Directory { get; set; }

    /// <summary>Redis connection string when <see cref="Provider"/> is <see cref="DataProtectionKeyRingProvider.Redis"/>.</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Redis key namespace prefix (optional).</summary>
    public string RedisKeyPrefix { get; set; } = "Zahy-DataProtection-Keys";
}

public enum DataProtectionKeyRingProvider
{
    LocalFileSystem = 0,
    Redis = 1,
    EntityFrameworkCore = 2
}
