using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Domain.Abstractions;

public interface IProductRepository
{
    /// <summary>Stores a newly registered product and assigns its identity.</summary>
    void Add(Product product);
}
