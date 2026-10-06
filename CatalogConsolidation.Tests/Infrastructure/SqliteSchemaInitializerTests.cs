using CatalogConsolidation.Infrastructure;
using CatalogConsolidation.Tests.Support;

namespace CatalogConsolidation.Tests.Infrastructure;

public sealed class SqliteSchemaInitializerTests : IDisposable
{
    private readonly TempCatalog _catalog = new();

    [Fact]
    public void Migrates_the_original_schema_to_the_documented_shape()
    {
        SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);

        Assert.Equal("TEXT", _catalog.Scalar<string>("SELECT type FROM pragma_table_info('SellerProduct') WHERE name = 'SellerProductId'"));
        Assert.Equal(1, _catalog.Scalar<int>("SELECT COUNT(*) FROM pragma_table_info('SellerProduct') WHERE name = 'SellerProductName'"));
        Assert.Equal(1, _catalog.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'UX_SellerProduct_SellerName_ProductId'"));
    }

    [Fact]
    public void Unique_index_rejects_a_second_link_for_the_same_seller_and_product()
    {
        SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);
        _catalog.Execute("INSERT INTO SellerProduct (SellerName, ProductId, SellerProductId) VALUES ('S', 1, 'a')");

        var ex = Assert.ThrowsAny<Exception>(
            () => _catalog.Execute("INSERT INTO SellerProduct (SellerName, ProductId, SellerProductId) VALUES ('S', 1, 'b')"));

        Assert.Contains("UNIQUE", ex.Message);
    }

    [Fact]
    public void Running_twice_does_not_rebuild_the_table_or_lose_rows()
    {
        SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);
        _catalog.Execute("INSERT INTO SellerProduct (SellerName, ProductId, SellerProductId, SellerProductName) VALUES ('S', 1, 'a', 'n')");

        SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);

        Assert.Equal(1, _catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct"));
    }

    [Fact]
    public void Existing_rows_survive_the_rebuild_and_their_numeric_ids_become_text()
    {
        // The original schema still has SellerProductId INTEGER, so this row is a legacy one.
        _catalog.Execute("INSERT INTO SellerProduct (SellerName, ProductId, SellerProductId) VALUES ('Legacy', 1, 42)");

        SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);

        Assert.Equal("42", _catalog.Scalar<string>("SELECT SellerProductId FROM SellerProduct WHERE SellerName = 'Legacy'"));
        Assert.Equal("text", _catalog.Scalar<string>("SELECT typeof(SellerProductId) FROM SellerProduct WHERE SellerName = 'Legacy'"));
    }

    [Fact]
    public void A_missing_database_fails_clearly_and_is_not_created()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.db");
        var connectionString = CatalogConnectionStringResolver.Resolve($"Data Source={missingPath};Pooling=False", Path.GetTempPath());

        var ex = Assert.Throws<InvalidOperationException>(() => SqliteSchemaInitializer.EnsureSchema(connectionString));

        Assert.Contains("CatalogDatabase:ConnectionString", ex.Message);
        Assert.False(File.Exists(missingPath));
    }

    public void Dispose() => _catalog.Dispose();
}
