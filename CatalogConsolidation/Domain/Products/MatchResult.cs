namespace CatalogConsolidation.Domain.Products;

public enum MatchKind { Exact, Approximate, NameOnly }

/// <summary>The catalog product an incoming description was matched to, and how.</summary>
public sealed record MatchResult(Product Product, MatchKind Kind, double? Score)
{
    public static MatchResult Exact(Product product) => new(product, MatchKind.Exact, null);

    public static MatchResult Approximate(Product product, double score) => new(product, MatchKind.Approximate, score);

    /// <summary>Matched by name alone, so the brand may differ from the catalog's (<see cref="MatchStrategy.Name"/>).</summary>
    public static MatchResult NameOnly(Product product) => new(product, MatchKind.NameOnly, null);
}
