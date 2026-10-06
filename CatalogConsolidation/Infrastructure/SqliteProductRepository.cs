using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// Stores new products with parameterized queries only (README decision #6). It only persists:
/// making the stored product visible to matching is the snapshot's job.
/// </summary>
public sealed class SqliteProductRepository : IProductRepository
{
    private readonly SqliteConnection _connection;

    public SqliteProductRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public void Add(Product product)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Product (Name, Brand, Category) VALUES ($name, $brand, $category);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", product.Name);
        command.Parameters.AddWithValue("$brand", (object?)product.Brand ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)product.Category ?? DBNull.Value);

        product.AssignId((long)command.ExecuteScalar()!);
    }
}
