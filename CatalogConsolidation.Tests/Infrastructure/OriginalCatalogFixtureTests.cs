using CatalogConsolidation.Tests.Support;

namespace CatalogConsolidation.Tests.Infrastructure;

/// <summary>
/// Guards the fixture every database test starts from: it must stay the catalog exactly as the
/// challenge delivered it (README "Catalog Analysis"), or the numbers asserted elsewhere are meaningless.
/// </summary>
public sealed class OriginalCatalogFixtureTests : IDisposable
{
    private readonly TempCatalog _catalog = new();

    [Fact]
    public void Holds_the_975_original_products_and_no_seller_links()
    {
        Assert.Equal(975, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product"));
        Assert.Equal(119, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product WHERE Brand IS NULL"));
        Assert.Equal(34, _catalog.Scalar<int>("SELECT COUNT(*) FROM Product WHERE Category IS NULL"));
        Assert.Equal(0, _catalog.Scalar<int>("SELECT COUNT(*) FROM SellerProduct"));
    }

    [Fact]
    public void Still_has_the_original_schema_before_the_app_migrates_it()
    {
        Assert.Equal("INTEGER", _catalog.Scalar<string>("SELECT type FROM pragma_table_info('SellerProduct') WHERE name = 'SellerProductId'"));
        Assert.Equal(0, _catalog.Scalar<int>("SELECT COUNT(*) FROM pragma_table_info('SellerProduct') WHERE name = 'SellerProductName'"));
        Assert.Equal(0, _catalog.Scalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'index'"));
    }

    public void Dispose() => _catalog.Dispose();
}
