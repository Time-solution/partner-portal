using Microsoft.Data.Sqlite;

namespace Zahy.Finance;

internal sealed class ZahyFinanceTestSqliteConnection : SqliteConnection
{
    public ZahyFinanceTestSqliteConnection(string connectionString)
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
