using Microsoft.Data.Sqlite;

namespace Zahy.Commission;

internal sealed class ZahyCommissionTestSqliteConnection : SqliteConnection
{
    public ZahyCommissionTestSqliteConnection(string connectionString)
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
