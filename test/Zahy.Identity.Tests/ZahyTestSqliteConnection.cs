using Microsoft.Data.Sqlite;

namespace Zahy.Identity;

/// <summary>
/// A SQLite connection whose Close/Dispose are suppressed so the in-memory
/// database survives across units of work for the lifetime of a test.
/// </summary>
public class ZahyTestSqliteConnection : SqliteConnection
{
    public ZahyTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }

    public override void Close()
    {
        // Keep the in-memory database alive between operations.
    }

    public void RealClose()
    {
        base.Close();
    }

    protected override void Dispose(bool disposing)
    {
        // Suppress disposal; the test host owns the connection lifetime.
    }
}
