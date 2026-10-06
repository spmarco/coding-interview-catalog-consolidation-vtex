using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Sellers;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// Stores and reads SellerProduct links with parameterized queries only (README decision #6 — SQL
/// injection is prevented by never concatenating user input into SQL text). It only persists;
/// deciding what an incoming offer means for an existing link is the domain's job.
/// </summary>
public sealed class SqliteSellerLinkRepository : ISellerLinkRepository
{
    private readonly SqliteConnection _connection;

    public SqliteSellerLinkRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public SellerProduct? Find(SellerName seller, long productId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT Id, SellerProductId, SellerProductName
            FROM SellerProduct
            WHERE SellerName = $sellerName AND ProductId = $productId;
            """;
        command.Parameters.AddWithValue("$sellerName", seller.Value);
        command.Parameters.AddWithValue("$productId", productId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return SellerProduct.Restore(
            reader.GetInt64(0),
            seller,
            productId,
            SellerProductId.Create(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public void Add(SellerProduct link)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SellerProduct (SellerName, ProductId, SellerProductId, SellerProductName)
            VALUES ($sellerName, $productId, $sellerProductId, $sellerProductName);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$sellerName", link.Seller.Value);
        command.Parameters.AddWithValue("$productId", link.ProductId);
        command.Parameters.AddWithValue("$sellerProductId", link.SellerProductId.Value);
        command.Parameters.AddWithValue("$sellerProductName", (object?)link.SellerProductName ?? DBNull.Value);

        link.AssignId((long)command.ExecuteScalar()!);
    }
}
