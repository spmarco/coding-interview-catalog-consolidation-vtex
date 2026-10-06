using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Tests.Domain;

public class LevenshteinSimilarityTests
{
    [Fact]
    public void Identical_strings_have_similarity_one()
    {
        Assert.Equal(1.0, LevenshteinSimilarity.Similarity("router wifi 6 tp-link", "router wifi 6 tp-link"));
    }

    [Fact]
    public void Two_empty_strings_are_identical()
    {
        Assert.Equal(1.0, LevenshteinSimilarity.Similarity("", ""));
    }

    [Fact]
    public void Distance_matches_the_classic_kitten_sitting_example()
    {
        Assert.Equal(3, LevenshteinSimilarity.Distance("kitten", "sitting"));
    }

    [Theory]
    [InlineData("processador amd ryzen 9 7950x", "processor amd ryzen 9 7950x", 0.931)]
    [InlineData("roteador wifi 6 tp-link", "router wifi 6 tp-link", 0.826)]
    [InlineData("colander stainless steel", "ladle stainless steel", 0.792)]
    [InlineData("tennis racket adult", "tennis racket bag", 0.737)]
    public void Matches_the_pairs_documented_in_the_threshold_calibration(string a, string b, double expected)
    {
        Assert.Equal(expected, LevenshteinSimilarity.Similarity(a, b), precision: 3);
    }
}
