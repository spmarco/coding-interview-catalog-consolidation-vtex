using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Infrastructure;

/// <summary>
/// The in-memory implementation of <see cref="ICatalogSnapshot"/>: loads the full catalog once
/// from the given connection and indexes it by key, by brand and by name. It never writes to the
/// database. What makes two products "the same" lives in the domain.
/// </summary>
public sealed class SqliteCatalogSnapshot : ICatalogSnapshot
{
    private readonly Dictionary<ProductKey, Product> _byKey = [];
    private readonly Dictionary<string, List<Product>> _byBrand = [];
    private readonly Dictionary<string, List<Product>> _byName = [];

    public SqliteCatalogSnapshot(SqliteConnection connection)
    {
        Load(connection);
    }

    public Product? FindByKey(ProductKey key) => _byKey.GetValueOrDefault(key);

    public IReadOnlyList<Product> FindByBrand(ProductKey key)
        => key.HasBrand && _byBrand.TryGetValue(key.NormalizedBrand, out var products) ? products : [];

    public IReadOnlyList<Product> FindByName(ProductKey key)
        => _byName.TryGetValue(key.NormalizedName, out var products) ? products : [];

    public void Track(Product product)
    {
        if (!product.IsPersisted)
        {
            throw new InvalidOperationException("Only a product that is already stored can be tracked by the snapshot.");
        }

        Index(product);
    }

    private void Load(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Brand, Category FROM Product;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Index(Product.Restore(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
    }

    private void Index(Product product)
    {
        _byKey[product.Key] = product;
        Bucket(_byBrand, product.Key.NormalizedBrand).Add(product);
        Bucket(_byName, product.Key.NormalizedName).Add(product);
    }

    private static List<Product> Bucket(Dictionary<string, List<Product>> index, string value)
    {
        if (!index.TryGetValue(value, out var bucket))
        {
            bucket = [];
            index[value] = bucket;
        }

        return bucket;
    }
}
