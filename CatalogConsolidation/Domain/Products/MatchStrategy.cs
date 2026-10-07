namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// Which rule decides that an incoming description is a product the catalog already has. The
/// uploaded file chooses it (README "Input"); a file that says nothing gets the first value, the
/// two-stage matching the challenge is solved with.
/// </summary>
public enum MatchStrategy
{
    /// <summary>Normalized name + brand, then Levenshtein similarity within the same brand.</summary>
    NameAndBrandSimilarity,

    /// <summary>Normalized name + brand, and nothing else.</summary>
    NameAndBrand,

    /// <summary>Normalized name + brand, then the normalized name alone, across brands.</summary>
    Name
}
