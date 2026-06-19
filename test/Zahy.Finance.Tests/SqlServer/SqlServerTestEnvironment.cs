using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Zahy.Finance;

internal static class SqlServerTestEnvironment
{
    public const string ConnectionString =
        "Server=(LocalDb)\\MSSQLLocalDB;Database=ZahyFinanceConcurrencyTest;Trusted_Connection=True;TrustServerCertificate=True";

    public static bool IsAvailable()
    {
        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task EnsureFinanceSchemaAsync(IServiceProvider serviceProvider)
    {
        await serviceProvider
            .GetRequiredService<ZahyFinanceDbContext>()
            .Database
            .MigrateAsync();
    }
}
