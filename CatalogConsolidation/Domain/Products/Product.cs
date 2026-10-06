using CatalogConsolidation.Domain.Exceptions;

namespace CatalogConsolidation.Domain.Products;

/// <summary>
/// A row of the marketplace catalog. It is created already valid, knows its own identity key,
/// can tell how close another description is to it, and never changes afterwards — the catalog's
/// data always wins over what a seller sends (README decision #8). The only transition is the
/// one-time assignment of the database identity when the product is first stored.
/// </summary>
public sealed class Product
{
    private Product(long id, string name, string? brand, string? category)
    {
        Id = id;
        Name = name;
        Brand = brand;
        Category = category;
        Key = ProductKey.From(name, brand);
    }

    /// <summary>0 until the product is stored; positive afterwards.</summary>
    public long Id { get; private set; }

    public string Name { get; }

    public string? Brand { get; }

    public string? Category { get; }

    public ProductKey Key { get; }

    public bool IsPersisted => Id > 0;

    /// <summary>Builds a new, not yet stored product from seller-provided text.</summary>
    public static Product Register(string? name, string? brand, string? category)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidProductException("Product name is required.");
        }

        return new Product(0, name.Trim(), CleanOptional(brand), CleanOptional(category));
    }

    /// <summary>Rebuilds a product exactly as it is stored.</summary>
    public static Product Restore(long id, string name, string? brand, string? category)
    {
        if (id <= 0)
        {
            throw new InvalidProductException($"A stored product must have a positive Id. Received: {id}.");
        }

        return new Product(id, name, brand, category);
    }

    public void AssignId(long id)
    {
        if (IsPersisted)
        {
            throw new InvalidOperationException($"Product {Id} already has an identity.");
        }

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "A product identity must be positive.");
        }

        Id = id;
    }

    /// <summary>
    /// How similar this product's name is to <paramref name="key"/>, in [0, 1]. Only products of the
    /// same (non-empty) brand are comparable; anything else scores 0 so it can never be merged.
    /// </summary>
    public double SimilarityTo(ProductKey key) => Key.IsSameBrandAs(key) ? Key.NameSimilarityTo(key) : 0.0;

    /// <summary>True when a seller sends a category that differs from the catalog's (reported, never applied).</summary>
    public bool HasDivergentCategory(string? sellerCategory)
        => !string.IsNullOrWhiteSpace(sellerCategory)
           && TextNormalizer.Normalize(sellerCategory) != TextNormalizer.Normalize(Category);

    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
