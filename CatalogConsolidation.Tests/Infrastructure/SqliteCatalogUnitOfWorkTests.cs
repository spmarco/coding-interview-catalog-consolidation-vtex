using CatalogConsolidation.Application.Exceptions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;
using CatalogConsolidation.Infrastructure;
using CatalogConsolidation.Tests.Support;
using Microsoft.Data.Sqlite;

namespace CatalogConsolidation.Tests.Infrastructure;

public sealed class SqliteCatalogUnitOfWorkTests : IDisposable
{
    private const int SqliteBusy = 5;

    private readonly TempCatalog _catalog = new();

    public SqliteCatalogUnitOfWorkTests() => SqliteSchemaInitializer.EnsureSchema(_catalog.ConnectionString);

    [Fact]
    public void Malicious_text_is_stored_literally_and_the_seller_name_is_kept_as_sent()
    {
        using (var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString))
        {
            var product = Product.Register("Security Test Product", "TestBrand'; SELECT 1; --", "Electronics");
            unitOfWork.Products.Add(product);
            unitOfWork.SellerLinks.Add(SellerProduct.Link(SellerName.Create("S"), product, SellerProductId.Create("id-1"), "Name  as   sent"));
            unitOfWork.Commit();
        }

        Assert.Equal("TestBrand'; SELECT 1; --", _catalog.Scalar<string>("SELECT Brand FROM Product WHERE Name = 'Security Test Product'"));
        Assert.Equal("Name  as   sent", _catalog.Scalar<string>("SELECT SellerProductName FROM SellerProduct WHERE SellerName = 'S'"));
        Assert.Equal(976, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product"));
    }

    [Fact]
    public void Storing_a_product_assigns_its_identity_but_the_snapshot_only_sees_it_once_tracked()
    {
        using var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);
        var product = Product.Register("Câmera Nova", "Canon", "Photography");
        var key = ProductKey.From("camera nova", "CANON");

        unitOfWork.Products.Add(product);

        Assert.Equal(976, product.Id);
        Assert.Null(unitOfWork.Catalog.FindByKey(key)); // the repository does not touch the snapshot

        unitOfWork.Catalog.Track(product);

        Assert.Same(product, unitOfWork.Catalog.FindByKey(key));
        Assert.Contains(product, unitOfWork.Catalog.FindByBrand(ProductKey.From("anything", "canon")));
    }

    [Fact]
    public void The_snapshot_refuses_to_track_a_product_that_was_never_stored()
    {
        using var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);

        Assert.Throws<InvalidOperationException>(() => unitOfWork.Catalog.Track(Product.Register("Not Stored", "Brand", null)));
    }

    [Fact]
    public void The_loaded_catalog_is_matched_by_key_and_by_brand()
    {
        using var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);

        var router = unitOfWork.Catalog.FindByKey(ProductKey.From("router  wifi 6 tp-link", "TP-LINK"));

        Assert.NotNull(router);
        Assert.Equal("Router WiFi 6 TP-Link", router.Name);
        Assert.Contains(router, unitOfWork.Catalog.FindByBrand(router.Key));
        Assert.Empty(unitOfWork.Catalog.FindByBrand(ProductKey.From("anything", null)));
    }

    [Fact]
    public void The_loaded_catalog_is_also_matched_by_name_alone_across_brands()
    {
        using var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);
        var router = unitOfWork.Catalog.FindByKey(ProductKey.From("Router WiFi 6 TP-Link", "TP-Link"))!;

        // The brand in the key is ignored by this lookup: that is what MatchStrategy.Name needs.
        Assert.Contains(router, unitOfWork.Catalog.FindByName(ProductKey.From("router  wifi 6 tp-link", "Netgear")));
        Assert.Empty(unitOfWork.Catalog.FindByName(ProductKey.From("no product has this name", null)));

        var stored = Product.Register("Brand New Thing", "Whatever", null);
        unitOfWork.Products.Add(stored);
        unitOfWork.Catalog.Track(stored);

        Assert.Contains(stored, unitOfWork.Catalog.FindByName(ProductKey.From("brand new thing", "Another")));
    }

    [Fact]
    public void A_stored_link_can_be_found_again_with_everything_it_was_saved_with()
    {
        using (var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString))
        {
            var product = unitOfWork.Catalog.FindByKey(ProductKey.From("Router WiFi 6 TP-Link", "TP-Link"))!;
            unitOfWork.SellerLinks.Add(SellerProduct.Link(SellerName.Create("SuperMart"), product, SellerProductId.Create("abc-1"), "Roteador  WiFi"));
            unitOfWork.Commit();
        }

        using var reader = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);
        var found = reader.SellerLinks.Find(SellerName.Create("SuperMart"), 21);

        Assert.NotNull(found);
        Assert.True(found.IsPersisted);
        Assert.Equal("abc-1", found.SellerProductId.Value);
        Assert.Equal("Roteador  WiFi", found.SellerProductName);
        Assert.Null(reader.SellerLinks.Find(SellerName.Create("OtherSeller"), 21));
    }

    [Fact]
    public void Disposing_without_commit_rolls_everything_back()
    {
        using (var unitOfWork = new SqliteCatalogUnitOfWork(_catalog.ConnectionString))
        {
            var product = Product.Register("Never Committed", "Brand", null);
            unitOfWork.Products.Add(product);
            unitOfWork.SellerLinks.Add(SellerProduct.Link(SellerName.Create("S"), product, SellerProductId.Create("id-1"), null));
        }

        Assert.Equal(975, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product"));
        Assert.Equal(0, _catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct"));
    }

    [Fact]
    public void A_second_unit_of_work_cannot_start_while_the_first_holds_the_write_lock()
    {
        var shortTimeout = _catalog.ConnectionStringWith("Default Timeout=1");

        using var first = new SqliteCatalogUnitOfWork(_catalog.ConnectionString);

        // BEGIN IMMEDIATE takes the write lock up front, so the second import waits and then
        // reports the catalog as busy instead of racing the first one.
        var ex = Assert.Throws<CatalogBusyException>(() => new SqliteCatalogUnitOfWork(shortTimeout));
        Assert.Equal(SqliteBusy, Assert.IsType<SqliteException>(ex.InnerException).SqliteErrorCode);

        first.Commit();

        using var third = new SqliteCatalogUnitOfWork(shortTimeout);
        third.Commit();
    }

    [Fact]
    public void A_commit_blocked_by_another_connections_reader_reports_the_catalog_as_busy_and_stores_nothing()
    {
        var shortTimeout = _catalog.ConnectionStringWith("Default Timeout=1");

        // An unfinished reader keeps a SHARED lock: BEGIN IMMEDIATE does not conflict with it,
        // but the COMMIT needs the exclusive lock and cannot get it.
        using var otherConnection = new SqliteConnection(_catalog.ConnectionString);
        otherConnection.Open();
        using var select = otherConnection.CreateCommand();
        select.CommandText = "SELECT Id FROM Product";
        using var unfinishedReader = select.ExecuteReader();
        unfinishedReader.Read();

        using (var unitOfWork = new SqliteCatalogUnitOfWork(shortTimeout))
        {
            unitOfWork.Products.Add(Product.Register("Blocked Commit", "Brand", null));

            var ex = Assert.Throws<CatalogBusyException>(() => unitOfWork.Commit());
            Assert.Equal(SqliteBusy, Assert.IsType<SqliteException>(ex.InnerException).SqliteErrorCode);
        }

        Assert.Equal(975, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product")); // disposed without a commit: rolled back
    }

    public void Dispose() => _catalog.Dispose();
}
