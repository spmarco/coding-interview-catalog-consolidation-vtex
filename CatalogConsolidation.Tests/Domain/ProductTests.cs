using CatalogConsolidation.Domain.Exceptions;
using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Tests.Domain;

public class ProductTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registering_a_product_without_a_name_is_rejected(string? name)
    {
        Assert.Throws<InvalidProductException>(() => Product.Register(name, "Brand", "Category"));
    }

    [Fact]
    public void Registering_trims_the_text_and_turns_blank_brand_and_category_into_null()
    {
        var product = Product.Register("  Camera Canon  ", "   ", "");

        Assert.Equal("Camera Canon", product.Name);
        Assert.Null(product.Brand);
        Assert.Null(product.Category);
    }

    [Fact]
    public void A_registered_product_is_not_persisted_until_it_gets_an_identity()
    {
        var product = Product.Register("Name", null, null);

        Assert.False(product.IsPersisted);

        product.AssignId(7);

        Assert.True(product.IsPersisted);
        Assert.Equal(7, product.Id);
    }

    [Fact]
    public void The_identity_can_only_be_assigned_once()
    {
        var product = Product.Register("Name", null, null);
        product.AssignId(7);

        Assert.Throws<InvalidOperationException>(() => product.AssignId(8));
    }

    [Fact]
    public void The_identity_must_be_positive()
    {
        var product = Product.Register("Name", null, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.AssignId(0));
    }

    [Fact]
    public void A_restored_product_keeps_exactly_what_was_stored()
    {
        var product = Product.Restore(3, "Smartphone  Galaxy S23 ", "Samsung", "Electronics");

        Assert.True(product.IsPersisted);
        Assert.Equal("Smartphone  Galaxy S23 ", product.Name);
    }

    [Fact]
    public void Restoring_requires_a_positive_identity()
    {
        Assert.Throws<InvalidProductException>(() => Product.Restore(0, "Name", null, null));
    }

    [Fact]
    public void The_key_ignores_case_accents_spacing_quotes_and_the_category()
    {
        var catalog = Product.Register("Câmera  Canon EOS R6", "Canon", "Photography");
        var seller = Product.Register("camera canon eos r6", "CANON", "Photo");

        Assert.Equal(catalog.Key, seller.Key);
    }

    [Fact]
    public void Products_of_different_brands_have_different_keys()
    {
        Assert.NotEqual(
            Product.Register("Widget", "BrandA", null).Key,
            Product.Register("Widget", "BrandB", null).Key);
    }

    [Fact]
    public void Similarity_is_only_computed_inside_the_same_brand()
    {
        var product = Product.Restore(1, "Router WiFi 6 TP-Link", "TP-Link", null);

        Assert.True(product.SimilarityTo(ProductKey.From("Roteador WiFi 6 TP-Link", "TP-Link")) > 0.8);
        Assert.Equal(0.0, product.SimilarityTo(ProductKey.From("Router WiFi 6 TP-Link", "Other")));
    }

    [Fact]
    public void A_product_without_a_brand_is_never_similar_to_anything()
    {
        var product = Product.Restore(1, "Round Rug 6 Feet", null, null);

        Assert.Equal(0.0, product.SimilarityTo(ProductKey.From("Round Rug 6 Feet", null)));
    }

    [Theory]
    [InlineData("Photography", null, false)]
    [InlineData("Photography", "", false)]
    [InlineData("Photography", "  photography ", false)]
    [InlineData("Photography", "Photo", true)]
    [InlineData(null, "Photo", true)]
    public void Category_divergence_compares_the_normalized_text_and_ignores_blanks(string? catalogCategory, string? sellerCategory, bool expected)
    {
        var product = Product.Restore(1, "Camera", "Canon", catalogCategory);

        Assert.Equal(expected, product.HasDivergentCategory(sellerCategory));
    }
}
