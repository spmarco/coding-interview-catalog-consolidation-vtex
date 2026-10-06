namespace CatalogConsolidation.Domain.Abstractions;

public interface ICatalogUnitOfWorkFactory
{
    /// <summary>Opens a transaction that holds the catalog's write lock until it is committed or disposed.</summary>
    ICatalogUnitOfWork Begin();
}
