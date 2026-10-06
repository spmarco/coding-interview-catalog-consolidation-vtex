namespace CatalogConsolidation.Domain.Sellers;

public enum LinkOutcome
{
    /// <summary>A new SellerProduct link was stored.</summary>
    Created,

    /// <summary>The seller is already linked to this product under the same SellerProductId (idempotent reprocessing).</summary>
    AlreadyLinked,

    /// <summary>The seller is already linked to this product under a different SellerProductId; the new one was discarded (README decision #7).</summary>
    DuplicateDiscarded
}
