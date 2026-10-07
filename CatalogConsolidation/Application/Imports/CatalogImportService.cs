using CatalogConsolidation.Application.Entries;
using CatalogConsolidation.Domain.Abstractions;

namespace CatalogConsolidation.Application.Imports;

public sealed class CatalogImportService : ICatalogImportService
{
    /// <summary>Read from appsettings; falls back to README's calibrated default when absent.</summary>
    private const string SimilarityThresholdKey = "Matching:SimilarityThreshold";
    private const double DefaultSimilarityThreshold = 0.81;

    private readonly ICatalogUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IConfiguration _configuration;

    public CatalogImportService(ICatalogUnitOfWorkFactory unitOfWorkFactory, IConfiguration configuration)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _configuration = configuration;
    }

    public async Task<ImportReport> ImportAsync(Stream fileContent, CancellationToken cancellationToken)
    {
        // The upload is read before the transaction opens, so a slow or invalid upload never holds the write lock.
        var file = await fileContent.ReadImportFileAsync(cancellationToken);

        // GetValue parses with the invariant culture, so "0.81" reads the same under any locale.
        var similarityThreshold = _configuration.GetValue(SimilarityThresholdKey, DefaultSimilarityThreshold);

        using var unitOfWork = _unitOfWorkFactory.Begin();

        var importer = new CatalogImporter(unitOfWork.Catalog,
                                           unitOfWork.Products,
                                           unitOfWork.SellerLinks,
                                           similarityThreshold,
                                           file.Strategy);
        var report = importer.Import(file.Entries);

        if (file.UnrecognizedStrategy is not null)
        {
            report.Warnings.Add(new ImportWarningReport(
                null,
                $"Unknown matchStrategy '{file.UnrecognizedStrategy}'; the file was imported with the default rule."));
        }

        unitOfWork.Commit();
        return report;
    }
}
