namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// What makes two product descriptions "the same product" (README decisions #2 and #3): the
/// normalized name plus the normalized brand. The category is deliberately not part of it.
/// Value object: two keys are equal when both normalized parts are equal.
/// </summary>
public sealed record ProductKey
{
    private ProductKey(string normalizedName, string normalizedBrand)
    {
        NormalizedName = normalizedName;
        NormalizedBrand = normalizedBrand;
    }

    public string NormalizedName { get; }

    public string NormalizedBrand { get; }

    /// <summary>A brand is what scopes approximate matching; without one there is no comparison group.</summary>
    public bool HasBrand => NormalizedBrand.Length > 0;

    public static ProductKey From(string name, string? brand)
        => new(TextNormalizer.Normalize(name), TextNormalizer.Normalize(brand));

    public bool IsSameBrandAs(ProductKey other) => HasBrand && NormalizedBrand == other.NormalizedBrand;

    public double NameSimilarityTo(ProductKey other)
        => LevenshteinSimilarity.Similarity(NormalizedName, other.NormalizedName);
}
