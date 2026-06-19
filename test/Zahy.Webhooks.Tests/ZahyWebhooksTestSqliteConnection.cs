using Microsoft.Data.Sqlite;

namespace Zahy.Webhooks;

public sealed class ZahyWebhooksTestSqliteConnection : SqliteConnection
{
    public ZahyWebhooksTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }
}
