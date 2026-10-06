using CatalogConsolidation.Domain.Exceptions;

namespace CatalogConsolidation.Domain.Sellers;

/// <summary>
/// A seller's name. Only trimmed and then compared exactly, so differently cased or spelled
/// names are different sellers (README decision #10).
/// </summary>
public sealed record SellerName
{
    private SellerName(string value) => Value = value;

    public string Value { get; }

    public static SellerName Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidSellerNameException("Seller name is required.");
        }

        return new SellerName(raw.Trim());
    }

    public override string ToString() => Value;
}
