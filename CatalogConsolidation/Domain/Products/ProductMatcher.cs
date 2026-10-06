using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// Implements the two-stage matching described in README's "How it works": an exact match on the
/// product key, falling back to Levenshtein similarity within the same brand.
/// </summary>
public sealed class ProductMatcher
{
    private readonly ICatalogSnapshot _catalog;
    private readonly double _similarityThreshold;

    public ProductMatcher(ICatalogSnapshot catalog, double similarityThreshold)
    {
        _catalog = catalog;
        _similarityThreshold = similarityThreshold;
    }

    public MatchResult? Match(ProductKey key)
    {
        var exact = _catalog.FindByKey(key);
        if (exact is not null)
        {
            return MatchResult.Exact(exact);
        }

        // Approximate matching needs a brand to scope the comparison group; without one there is
        // no reliable group to compare against (see README Known Limitations).
        if (!key.HasBrand)
        {
            return null;
        }

        Product? best = null;
        var bestScore = -1.0;

        foreach (var candidate in _catalog.FindByBrand(key))
        {
            var score = candidate.SimilarityTo(key);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best is not null && bestScore >= _similarityThreshold
            ? MatchResult.Approximate(best, bestScore)
            : null;
    }
}
