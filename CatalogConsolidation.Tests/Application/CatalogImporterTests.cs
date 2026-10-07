using CatalogConsolidation.Application.Entries;
using CatalogConsolidation.Application.Imports;
using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using CatalogConsolidation.Domain.Sellers;
using CatalogConsolidation.Tests.Support;
using NSubstitute;

namespace CatalogConsolidation.Tests.Application;

public class CatalogImporterTests
{
    private const double Threshold = 0.81;

    private readonly ICatalogSnapshot _catalog = StatefulMocks.Catalog();
    private readonly IProductRepository _products = StatefulMocks.Products();
    private readonly ISellerLinkRepository _links = StatefulMocks.SellerLinks();
    private readonly CatalogImporter _importer;

    public CatalogImporterTests()
    {
        _importer = ImporterFor(_catalog);
    }

    /// <summary>For the tests whose scenario starts with products already in the catalog.</summary>
    private CatalogImporter ImporterFor(ICatalogSnapshot catalog,
                                        MatchStrategy strategy = MatchStrategy.NameAndBrandSimilarity)
        => new(catalog, _products, _links, Threshold, strategy);

    private static ProductEntryDto Entry(string? id, string? seller, string? name, string? brand = "Brand", string? category = "Cat")
        => new(id, seller, name, brand, category);

    private static string NewId() => Guid.NewGuid().ToString();

    [Fact]
    public void Rejects_a_null_row_without_stopping_the_import()
    {
        var report = _importer.Import([null, Entry(NewId(), "Seller", "New Product")]);

        Assert.Single(report.RejectedRows);
        Assert.Equal(1, report.ProductsCreated);
    }

    [Theory]
    [InlineData(null, "Seller", "Name", "Seller product id is required.")]
    [InlineData("id", "   ", "Name", "Seller name is required.")]
    [InlineData("id", "Seller", "", "Product name is required.")]
    public void Rejects_rows_missing_a_required_field_and_says_why(string? id, string? seller, string? name, string reason)
    {
        var report = _importer.Import([Entry(id, seller, name)]);

        var rejected = Assert.Single(report.RejectedRows);
        Assert.Equal(reason, rejected.Reason);
        Assert.Equal(0, report.ProductsCreated);
    }

    [Fact]
    public void A_rejected_row_stores_nothing_at_all()
    {
        // Valid seller and id, blank product name: nothing may be stored for a row that is rejected.
        _importer.Import([Entry(NewId(), "Seller", "  ")]);

        _products.DidNotReceive().Add(Arg.Any<Product>());
        _catalog.DidNotReceive().Track(Arg.Any<Product>());
        _links.DidNotReceive().Add(Arg.Any<SellerProduct>());
    }

    [Fact]
    public void Warns_but_still_processes_a_non_guid_shaped_id()
    {
        var report = _importer.Import([Entry("not-a-guid", "Seller", "New Product")]);

        Assert.Equal(1, report.ProductsCreated);
        Assert.Contains(report.Warnings, w => w.Message.Contains("GUID"));
    }

    [Fact]
    public void A_new_product_is_stored_first_and_then_tracked_by_the_snapshot()
    {
        _importer.Import([Entry(NewId(), "Seller", "New Product")]);

        // The snapshot may only be told about a product that is already stored (it has its identity).
        Received.InOrder(() =>
        {
            _products.Add(Arg.Is<Product>(p => p.Name == "New Product"));
            _catalog.Track(Arg.Is<Product>(p => p.Name == "New Product" && p.IsPersisted));
        });
    }

    [Fact]
    public void Matched_product_fields_are_never_altered_and_nothing_is_stored()
    {
        var camera = Product.Restore(1, "Camera Canon EOS R6", "Canon", "Photography");
        var catalog = StatefulMocks.Catalog(camera);

        var report = ImporterFor(catalog).Import([Entry(NewId(), "Seller", "Camera Canon EOS R6", "Canon", "Photo")]);

        Assert.Equal(1, report.ExactMatches);
        _products.DidNotReceive().Add(Arg.Any<Product>());
        catalog.DidNotReceive().Track(Arg.Any<Product>());
        Assert.Equal("Photography", camera.Category); // untouched
        Assert.Contains(report.Warnings, w => w.Message.Contains("Category mismatch"));
    }

    [Fact]
    public void A_matching_category_raises_no_warning()
    {
        var catalog = StatefulMocks.Catalog(Product.Restore(1, "Camera Canon EOS R6", "Canon", "Photography"));

        var report = ImporterFor(catalog).Import([Entry(NewId(), "Seller", "Camera Canon EOS R6", "Canon", "photography")]);

        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void Reprocessing_the_identical_row_creates_nothing_new()
    {
        var entry = Entry(NewId(), "Seller", "New Product");

        var firstReport = _importer.Import([entry]);
        var secondReport = _importer.Import([entry]);

        Assert.Equal(1, firstReport.ProductsCreated);
        Assert.Equal(1, firstReport.LinksCreated);
        Assert.Equal(0, secondReport.ProductsCreated); // second run matches the product created by the first
        Assert.Equal(0, secondReport.LinksCreated);
        Assert.Empty(secondReport.DuplicateLinksIgnored);
    }

    [Fact]
    public void A_different_SellerProductId_for_an_already_linked_product_is_reported_not_duplicated()
    {
        var importer = ImporterFor(StatefulMocks.Catalog(Product.Restore(1, "Garden Hose 50 Feet", "Flexzilla", null)));

        importer.Import([Entry("id-1", "SuperMart", "Garden Hose 50 Feet", "Flexzilla", null)]);
        var report = importer.Import([Entry("id-2", "SuperMart", "Garden Hose 50 Feet", "Flexzilla", null)]);

        Assert.Equal(0, report.LinksCreated);
        var ignored = Assert.Single(report.DuplicateLinksIgnored);
        Assert.Equal("SuperMart", ignored.SellerName);
        Assert.Equal("Garden Hose 50 Feet", ignored.ProductName);
        Assert.Equal("id-2", ignored.DiscardedSellerProductId);
        Assert.Equal("id-1", ignored.ExistingSellerProductId);
        _links.Received(1).Add(Arg.Any<SellerProduct>()); // only the first link was ever stored
    }

    [Fact]
    public void Another_seller_offering_the_same_product_gets_its_own_link()
    {
        var importer = ImporterFor(StatefulMocks.Catalog(Product.Restore(1, "Garden Hose 50 Feet", "Flexzilla", null)));

        var report = importer.Import([Entry("id-1", "SuperMart", "Garden Hose 50 Feet", "Flexzilla", null),
                                      Entry("id-1", "TechWorld", "Garden Hose 50 Feet", "Flexzilla", null)]);

        Assert.Equal(2, report.LinksCreated);
        Assert.Empty(report.DuplicateLinksIgnored);
    }

    [Fact]
    public void The_sellers_own_name_for_the_product_is_kept_exactly_as_sent()
    {
        var importer = ImporterFor(StatefulMocks.Catalog(Product.Restore(1, "Smartphone Galaxy S23", "Samsung", "Electronics")));

        importer.Import([Entry(NewId(), "Seller", "Smartphone  Galaxy S23", "Samsung", "Electronics")]);

        _links.Received(1).Add(Arg.Is<SellerProduct>(l => l.SellerProductName == "Smartphone  Galaxy S23"));
    }

    [Fact]
    public void Approximate_match_within_the_same_run_is_seen_by_later_rows()
    {
        var report = _importer.Import(
        [
            Entry(NewId(), "SellerA", "Roteador WiFi 6 TP-Link", "TP-Link", "Networking"),
            Entry(NewId(), "SellerB", "Router WiFi 6 TP-Link", "TP-Link", "Networking")
        ]);

        // Neither row exists in the catalog; the first creates the product, the second must
        // find it via approximate matching against a product created earlier in this run.
        Assert.Equal(1, report.ProductsCreated);
        _products.Received(1).Add(Arg.Any<Product>());
        var approximate = Assert.Single(report.ApproximateMatches);
        Assert.Equal("Roteador WiFi 6 TP-Link", approximate.MatchedProductName);
        Assert.Equal(2, report.LinksCreated);
    }

    [Fact]
    public void The_name_strategy_links_to_another_brands_product_and_warns_about_it()
    {
        var catalog = StatefulMocks.Catalog(Product.Restore(1, "Wireless Mouse", "Logitech", "Computers"));

        var report = ImporterFor(catalog, MatchStrategy.Name)
            .Import([Entry(NewId(), "SuperMart", "Wireless Mouse", "Microsoft", "Computers")]);

        Assert.Equal(1, report.NameOnlyMatches);
        Assert.Equal(0, report.ProductsCreated);
        Assert.Equal(0, report.ExactMatches);
        Assert.Empty(report.ApproximateMatches);
        _products.DidNotReceive().Add(Arg.Any<Product>());
        _links.Received(1).Add(Arg.Is<SellerProduct>(l => l.ProductId == 1));

        var warning = Assert.Single(report.Warnings);
        Assert.Contains("Matched by name only", warning.Message);
        Assert.Contains("Logitech", warning.Message);
        Assert.Contains("Microsoft", warning.Message);
    }

    [Fact]
    public void The_name_and_brand_strategy_creates_the_product_the_default_would_have_merged()
    {
        var catalog = StatefulMocks.Catalog(Product.Restore(1, "Roteador WiFi 6 TP-Link", "TP-Link", "Networking"));

        var report = ImporterFor(catalog, MatchStrategy.NameAndBrand)
            .Import([Entry(NewId(), "SuperMart", "Router WiFi 6 TP-Link", "TP-Link", "Networking")]);

        // The default strategy matches this pair approximately (0.826); this one must not.
        Assert.Equal(1, report.ProductsCreated);
        Assert.Empty(report.ApproximateMatches);
        Assert.Equal(0, report.NameOnlyMatches);
    }

    [Fact]
    public void An_exact_match_still_wins_before_the_strategy_is_consulted()
    {
        var catalog = StatefulMocks.Catalog(Product.Restore(1, "Wireless Mouse", "Logitech", "Computers"));

        var report = ImporterFor(catalog, MatchStrategy.Name)
            .Import([Entry(NewId(), "SuperMart", "Wireless Mouse", "Logitech", "Computers")]);

        Assert.Equal(1, report.ExactMatches);
        Assert.Equal(0, report.NameOnlyMatches);
        Assert.Empty(report.Warnings);
    }
}
