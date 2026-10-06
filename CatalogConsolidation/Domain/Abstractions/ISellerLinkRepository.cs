using CatalogConsolidation.Domain.Sellers;

namespace CatalogConsolidation.Domain.Abstractions;

public interface ISellerLinkRepository
{
    /// <summary>The link between this seller and this product, or null when the seller does not offer it yet.</summary>
    SellerProduct? Find(SellerName seller, long productId);

    /// <summary>Stores a new link and assigns its identity.</summary>
    void Add(SellerProduct link);
}
