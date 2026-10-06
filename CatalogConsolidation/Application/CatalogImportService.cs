using CatalogConsolidation.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace CatalogConsolidation.Application;

public sealed class CatalogImportService : ICatalogImportService
{
    private readonly ICatalogUnitOfWorkFactory _unitOfWorkFactory;
    private readonly MatchingOptions _matchingOptions;

    public CatalogImportService(ICatalogUnitOfWorkFactory unitOfWorkFactory, IOptions<MatchingOptions> matchingOptions)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _matchingOptions = matchingOptions.Value;
    }

    public async Task<ImportReport> ImportAsync(Stream fileContent, CancellationToken cancellationToken)
    {
        // The upload is read before the transaction opens, so a slow or invalid upload never holds the write lock.
        var entries = await fileContent.ReadProductEntriesAsync(cancellationToken);

        using var unitOfWork = _unitOfWorkFactory.Begin();

        var importer = new CatalogImporter(unitOfWork.Catalog, unitOfWork.Products, unitOfWork.SellerLinks, _matchingOptions);
        var report = importer.Import(entries);

        unitOfWork.Commit();
        return report;
    }
}
