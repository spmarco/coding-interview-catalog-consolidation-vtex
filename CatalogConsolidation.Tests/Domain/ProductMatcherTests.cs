using CatalogConsolidation.Domain.Abstractions;
using CatalogConsolidation.Domain.Products;
using NSubstitute;

namespace CatalogConsolidation.Tests.Domain;

public class ProductMatcherTests
{
    private const double Threshold = 0.81;

    // Same-length strings differing in exactly `differences` positions have Levenshtein
    // distance == differences (pure substitutions), so similarity = 1 - differences/100.
    private static readonly string Base = new('a', 100);

    private readonly ICatalogSnapshot _catalog = Substitute.For<ICatalogSnapshot>(); // FindByKey returns null: no exact match
    private readonly ProductMatcher _matcher;

    public ProductMatcherTests()
    {
        _matcher = new ProductMatcher(_catalog, Threshold);
    }

    private static string Variant(int differences)
    {
        var chars = Base.ToCharArray();
        for (var i = 0; i < differences; i++)
        {
            chars[i] = 'b';
        }

        return new string(chars);
    }

    /// <summary>Makes the brand lookup return <paramref name="brandProducts"/> whatever brand is asked for.</summary>
    private void GivenBrandProducts(params Product[] brandProducts)
        => _catalog.FindByBrand(Arg.Any<ProductKey>()).Returns(brandProducts);

    /// <summary>Makes the name lookup return <paramref name="namedProducts"/> whatever name is asked for.</summary>
    private void GivenProductsNamed(params Product[] namedProducts)
        => _catalog.FindByName(Arg.Any<ProductKey>()).Returns(namedProducts);

    private ProductMatcher MatcherFor(MatchStrategy strategy) => new(_catalog, Threshold, strategy);

    [Fact]
    public void Score_just_below_threshold_is_rejected()
    {
        GivenBrandProducts(Product.Restore(1, Variant(0), "BrandX", null));

        // distance 20 on a 100-char string => similarity 0.80, just under 0.81.
        var result = _matcher.Match(ProductKey.From(Variant(20), "BrandX"));

        Assert.Null(result);
    }

    [Fact]
    public void Score_just_above_threshold_is_accepted()
    {
        GivenBrandProducts(Product.Restore(1, Variant(0), "BrandX", null));

        // distance 18 on a 100-char string => similarity 0.82, just over 0.81.
        var result = _matcher.Match(ProductKey.From(Variant(18), "BrandX"));

        Assert.NotNull(result);
        Assert.Equal(MatchKind.Approximate, result.Kind);
        Assert.Equal(1, result.Product.Id);
        Assert.Equal(0.82, result.Score!.Value, precision: 3);
    }

    [Fact]
    public void Identical_name_under_a_different_brand_never_matches_even_if_the_catalog_returns_it()
    {
        // The worst case: a snapshot that hands back another brand's product. The domain rule must still refuse it.
        GivenBrandProducts(Product.Restore(1, "Widget", "BrandA", null));

        var result = _matcher.Match(ProductKey.From("Widget", "BrandB"));

        Assert.Null(result);
    }

    [Fact]
    public void Approximate_matching_never_applies_without_a_brand()
    {
        GivenBrandProducts(Product.Restore(1, "Round Rug 6 Feet", null, null));

        // A single added character away from a perfect match, but brand-less entries never
        // reach the approximate stage (README Known Limitations).
        var result = _matcher.Match(ProductKey.From("Round Rug 6 Feets", null));

        Assert.Null(result);
        _catalog.DidNotReceive().FindByBrand(Arg.Any<ProductKey>());
    }

    [Fact]
    public void An_exact_match_is_returned_without_even_looking_at_the_brand()
    {
        var router = Product.Restore(1, "Router WiFi 6 TP-Link", "TP-Link", null);
        var key = ProductKey.From("router wifi 6 tp-link", "tp-link");
        _catalog.FindByKey(key).Returns(router);

        var result = _matcher.Match(key);

        Assert.Equal(MatchKind.Exact, result!.Kind);
        Assert.Same(router, result.Product);
        _catalog.DidNotReceive().FindByBrand(Arg.Any<ProductKey>());
    }

    [Fact]
    public void The_closest_product_of_the_brand_wins_the_approximate_match()
    {
        GivenBrandProducts(
            Product.Restore(1, "Colander Stainless Steel", "OXO", null),
            Product.Restore(2, "Roteador WiFi 6 TP-Link", "OXO", null));

        var result = _matcher.Match(ProductKey.From("Router WiFi 6 TP-Link", "OXO"));

        Assert.Equal(2, result!.Product.Id);
    }

    [Fact]
    public void The_name_and_brand_strategy_stops_before_the_similarity_stage()
    {
        GivenBrandProducts(Product.Restore(1, Variant(0), "BrandX", null));

        // A pair the default strategy would accept (similarity 0.82): this one must not.
        var result = MatcherFor(MatchStrategy.NameAndBrand).Match(ProductKey.From(Variant(18), "BrandX"));

        Assert.Null(result);
        _catalog.DidNotReceive().FindByBrand(Arg.Any<ProductKey>());
        _catalog.DidNotReceive().FindByName(Arg.Any<ProductKey>());
    }

    [Fact]
    public void The_name_strategy_matches_a_product_of_another_brand()
    {
        GivenProductsNamed(Product.Restore(1, "Wireless Mouse", "Logitech", null));

        var result = MatcherFor(MatchStrategy.Name).Match(ProductKey.From("Wireless  Mouse", "Microsoft"));

        Assert.Equal(MatchKind.NameOnly, result!.Kind);
        Assert.Equal(1, result.Product.Id);
        Assert.Null(result.Score);
    }

    [Fact]
    public void The_name_strategy_takes_the_lowest_id_when_several_brands_share_the_name()
    {
        GivenProductsNamed(
            Product.Restore(7, "Wireless Mouse", "Logitech", null),
            Product.Restore(3, "Wireless Mouse", "Razer", null),
            Product.Restore(5, "Wireless Mouse", "Dell", null));

        var result = MatcherFor(MatchStrategy.Name).Match(ProductKey.From("Wireless Mouse", "Microsoft"));

        Assert.Equal(3, result!.Product.Id);
    }

    [Fact]
    public void The_name_strategy_matches_nothing_when_no_product_shares_the_name()
    {
        GivenProductsNamed();

        Assert.Null(MatcherFor(MatchStrategy.Name).Match(ProductKey.From("Wireless Mouse", "Microsoft")));
    }
}
