using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Domain.Sellers;

/// <summary>
/// The fact that a seller offers a catalog product, under the seller's own code. A seller is linked
/// to a given product at most once (unique on seller + product); the entity knows how to compare an
/// incoming offer against the link already on file.
/// </summary>
public sealed class SellerProduct
{
    private SellerProduct(long id, SellerName seller, long productId, SellerProductId sellerProductId, string? sellerProductName)
    {
        Id = id;
        Seller = seller;
        ProductId = productId;
        SellerProductId = sellerProductId;
        SellerProductName = sellerProductName;
    }

    /// <summary>0 until the link is stored; positive afterwards.</summary>
    public long Id { get; private set; }

    public SellerName Seller { get; }

    public long ProductId { get; }

    public SellerProductId SellerProductId { get; }

    /// <summary>The product name exactly as the seller sent it, kept for traceability.</summary>
    public string? SellerProductName { get; }

    public bool IsPersisted => Id > 0;

    /// <summary>Builds a new, not yet stored link between a seller and a product that is already in the catalog.</summary>
    public static SellerProduct Link(SellerName seller, Product product, SellerProductId sellerProductId, string? sellerProductName)
    {
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(sellerProductId);

        if (!product.IsPersisted)
        {
            throw new InvalidOperationException("A seller can only be linked to a product that is already in the catalog.");
        }

        return new SellerProduct(0, seller, product.Id, sellerProductId, sellerProductName);
    }

    /// <summary>Rebuilds a link exactly as it is stored.</summary>
    public static SellerProduct Restore(long id, SellerName seller, long productId, SellerProductId sellerProductId, string? sellerProductName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        return new SellerProduct(id, seller, productId, sellerProductId, sellerProductName);
    }

    public void AssignId(long id)
    {
        if (IsPersisted)
        {
            throw new InvalidOperationException($"Seller link {Id} already has an identity.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        Id = id;
    }

    /// <summary>
    /// Compares an offer by the same seller for the same product against this stored link:
    /// the same SellerProductId just repeats it, a different one must be discarded. The caller found
    /// this link by the incoming offer's seller and product, so both always match.
    /// </summary>
    public LinkOutcome Reconcile(SellerProduct incoming)
    {
        return incoming.SellerProductId == SellerProductId
            ? LinkOutcome.AlreadyLinked
            : LinkOutcome.DuplicateDiscarded;
    }
}
