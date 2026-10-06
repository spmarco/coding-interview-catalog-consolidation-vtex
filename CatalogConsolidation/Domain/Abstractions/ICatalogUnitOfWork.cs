namespace CatalogConsolidation.Domain.Abstractions;

/// <summary>
/// One import's transaction: everything stored through <see cref="Products"/> and <see cref="SellerLinks"/>
/// is persisted only if <see cref="Commit"/> is called; disposing without committing discards it all.
/// </summary>
public interface ICatalogUnitOfWork : IDisposable
{
    ICatalogSnapshot Catalog { get; }

    IProductRepository Products { get; }

    ISellerLinkRepository SellerLinks { get; }

    void Commit();
}
