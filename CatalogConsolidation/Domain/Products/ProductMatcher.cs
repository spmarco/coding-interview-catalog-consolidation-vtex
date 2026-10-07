using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// Implements the matching described in README's "How it works", as far as the file asked for
/// (<see cref="MatchStrategy"/>): the exact match on the product key always runs first, and what
/// happens when it finds nothing is what the strategy decides.
/// </summary>
public sealed class ProductMatcher
{
    private readonly ICatalogSnapshot _catalog;
    private readonly double _similarityThreshold;
    private readonly MatchStrategy _strategy;

    public ProductMatcher(ICatalogSnapshot catalog,
                          double similarityThreshold,
                          MatchStrategy strategy = MatchStrategy.NameAndBrandSimilarity)
    {
        _catalog = catalog;
        _similarityThreshold = similarityThreshold;
        _strategy = strategy;
    }

    public MatchResult? Match(ProductKey key)
    {
        var exact = _catalog.FindByKey(key);
        if (exact is not null)
        {
            return MatchResult.Exact(exact);
        }

        return _strategy switch
        {
            MatchStrategy.Name => MatchByNameAlone(key),
            MatchStrategy.NameAndBrandSimilarity => MatchBySimilarity(key),
            _ => null
        };
    }

    /// <summary>
    /// The name alone, ignoring the brand. The catalog may hold the same name under several
    /// brands, and nothing in the data says which one the seller meant: the lowest Id wins, which
    /// is a stable choice rather than a meaningful one (README Known Limitations).
    /// </summary>
    private MatchResult? MatchByNameAlone(ProductKey key)
    {
        Product? best = null;

        foreach (var candidate in _catalog.FindByName(key))
        {
            if (best is null || candidate.Id < best.Id)
            {
                best = candidate;
            }
        }

        return best is null ? null : MatchResult.NameOnly(best);
    }

    private MatchResult? MatchBySimilarity(ProductKey key)
    {
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
