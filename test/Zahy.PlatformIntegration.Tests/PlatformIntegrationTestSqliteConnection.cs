using Microsoft.Data.Sqlite;

namespace Zahy.PlatformIntegration;

public sealed class PlatformIntegrationTestSqliteConnection : SqliteConnection
{
    public PlatformIntegrationTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }
}
