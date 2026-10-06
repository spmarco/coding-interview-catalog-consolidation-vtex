using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// Applies README's "Database Changes" once at startup, idempotently: SellerProductId as TEXT,
/// a SellerProductName column, and a unique index on (SellerName, ProductId). SQLite has no
/// ALTER COLUMN, so changing SellerProductId's affinity requires the standard rebuild-the-table
/// pattern; this is safe to re-run because the checks below make it a no-op once applied.
/// </summary>
public static class SqliteSchemaInitializer
{
    public static void EnsureSchema(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException(
                $"Could not open the catalog database '{connection.DataSource}'. Check 'CatalogDatabase:ConnectionString'.", ex);
        }

        using var transaction = connection.BeginTransaction();

        if (!IsSellerProductIdText(connection) || !HasSellerProductNameColumn(connection))
        {
            RebuildSellerProductTable(connection);
        }

        EnsureUniqueIndex(connection);

        transaction.Commit();
    }

    private static bool IsSellerProductIdText(SqliteConnection connection)
    {
        foreach (var (name, type) in TableInfo(connection, "SellerProduct"))
        {
            if (string.Equals(name, "SellerProductId", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static bool HasSellerProductNameColumn(SqliteConnection connection)
    {
        foreach (var (name, _) in TableInfo(connection, "SellerProduct"))
        {
            if (string.Equals(name, "SellerProductName", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Name, string Type)> TableInfo(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return (reader.GetString(1), reader.GetString(2));
        }
    }

    private static void RebuildSellerProductTable(SqliteConnection connection)
    {
        Execute(connection, "DROP INDEX IF EXISTS UX_SellerProduct_SellerName_ProductId;");
        Execute(connection, """
            CREATE TABLE SellerProduct_New (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SellerName TEXT NOT NULL,
                ProductId INTEGER NOT NULL CONSTRAINT FK_Product_Id REFERENCES Product (Id),
                SellerProductId TEXT NOT NULL,
                SellerProductName TEXT NULL
            );
            """);
        Execute(connection, """
            INSERT INTO SellerProduct_New (Id, SellerName, ProductId, SellerProductId, SellerProductName)
            SELECT Id, SellerName, ProductId, CAST(SellerProductId AS TEXT), NULL FROM SellerProduct;
            """);
        Execute(connection, "DROP TABLE SellerProduct;");
        Execute(connection, "ALTER TABLE SellerProduct_New RENAME TO SellerProduct;");
    }

    private static void EnsureUniqueIndex(SqliteConnection connection)
    {
        Execute(connection, "CREATE UNIQUE INDEX IF NOT EXISTS UX_SellerProduct_SellerName_ProductId ON SellerProduct (SellerName, ProductId);");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
