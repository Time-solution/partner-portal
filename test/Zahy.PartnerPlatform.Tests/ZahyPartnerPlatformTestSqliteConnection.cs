using Microsoft.Data.Sqlite;

namespace Zahy.PartnerPlatform;

/// <summary>
/// SQLite connection whose Close/Dispose are suppressed so the in-memory
/// database survives across units of work for the lifetime of a test.
/// </summary>
public class ZahyPartnerPlatformTestSqliteConnection : SqliteConnection
{
    public ZahyPartnerPlatformTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }

    public override void Close()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
