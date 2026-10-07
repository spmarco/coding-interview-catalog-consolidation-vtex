using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Domain.Abstractions;

/// <summary>
/// A read-only, transaction-scoped view of the Product catalog used for matching: the rows that
/// existed when the import started, plus every product the import has <see cref="Track"/>ed since.
/// This is not a cache in the staleness/invalidation sense (see README "How it works"): it never
/// writes anything; whoever stores a new product tells it so, and from then on it is visible here.
/// </summary>
public interface ICatalogSnapshot
{
    /// <summary>The product with exactly this key, or null.</summary>
    Product? FindByKey(ProductKey key);

    /// <summary>The products of the key's brand (the approximate-matching comparison group); empty when the key has no brand.</summary>
    IReadOnlyList<Product> FindByBrand(ProductKey key);

    /// <summary>The products whose normalized name equals the key's, whatever their brand; empty when none does.</summary>
    IReadOnlyList<Product> FindByName(ProductKey key);

    /// <summary>Makes a product that was just stored (so it already has its identity) visible to later lookups.</summary>
    void Track(Product product);
}
