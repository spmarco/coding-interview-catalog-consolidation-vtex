namespace CatalogConsolidation.Application;

public interface ICatalogImportService
{
    /// <summary>Imports the products file in a single transaction and reports what happened to each entry.</summary>
    Task<ImportReport> ImportAsync(Stream fileContent, CancellationToken cancellationToken);
}
