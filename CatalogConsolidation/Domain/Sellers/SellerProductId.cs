using CatalogConsolidation.Domain.Exceptions;

namespace CatalogConsolidation.Domain.Sellers;

/// <summary>
/// The product code in the seller's own system (the file's <c>Id</c>). It is an opaque text chosen
/// by the seller: never used to find duplicates and never rejected for its format (README
/// decisions #1 and #5). Whether it looks like a GUID is only informational.
/// </summary>
public sealed record SellerProductId
{
    private SellerProductId(string value) => Value = value;

    public string Value { get; }

    public bool IsGuidShaped => Guid.TryParseExact(Value, "D", out _);

    public static SellerProductId Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidSellerProductIdException("Seller product id is required.");
        }

        return new SellerProductId(raw.Trim());
    }

    public override string ToString() => Value;
}
