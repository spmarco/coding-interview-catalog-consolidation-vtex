using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Tests.Domain;

public class TextNormalizerTests
{
    [Fact]
    public void Null_normalizes_to_empty_string()
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(null));
    }

    [Fact]
    public void Collapses_duplicate_whitespace()
    {
        Assert.Equal(TextNormalizer.Normalize("Smartphone Galaxy S23"), TextNormalizer.Normalize("Smartphone  Galaxy S23"));
    }

    [Fact]
    public void Removes_accents()
    {
        Assert.Equal(TextNormalizer.Normalize("Camera"), TextNormalizer.Normalize("Câmera"));
    }

    [Theory]
    [InlineData("Tablet iPad Pro 12.9\"")]
    [InlineData("Tablet iPad Pro 12.9''")]
    [InlineData("Tablet iPad Pro 12.9")]
    public void Double_quote_and_double_apostrophe_and_bare_inches_are_equivalent(string variant)
    {
        Assert.Equal("tablet ipad pro 12.9", TextNormalizer.Normalize(variant));
    }

    [Fact]
    public void Case_is_ignored()
    {
        Assert.Equal(TextNormalizer.Normalize("samsung"), TextNormalizer.Normalize("SAMSUNG"));
    }

    [Fact]
    public void Leading_and_trailing_whitespace_is_trimmed()
    {
        Assert.Equal(TextNormalizer.Normalize("Dell"), TextNormalizer.Normalize("  Dell  "));
    }
}
