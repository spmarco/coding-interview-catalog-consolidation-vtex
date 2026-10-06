namespace CatalogConsolidation.Domain.Products;

public enum MatchKind { Exact, Approximate }

/// <summary>The catalog product an incoming description was matched to, and how.</summary>
public sealed record MatchResult(Product Product, MatchKind Kind, double? Score)
{
    public static MatchResult Exact(Product product) => new(product, MatchKind.Exact, null);

    public static MatchResult Approximate(Product product, double score) => new(product, MatchKind.Approximate, score);
}
