using Microsoft.Data.Sqlite;

namespace Zahy.OrderLedger;

public sealed class ZahyOrderLedgerTestSqliteConnection : SqliteConnection
{
    public ZahyOrderLedgerTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }
}
