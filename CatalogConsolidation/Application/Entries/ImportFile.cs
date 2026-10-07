using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Application.Entries;

/// <summary>
/// An uploaded products file: the entries, plus the matching rule the file asked for. A file that
/// is a bare JSON array, or an object without "matchStrategy", gets
/// <see cref="MatchStrategy.NameAndBrandSimilarity"/> — the behavior of the challenge's own file.
/// <paramref name="UnrecognizedStrategy"/> holds the raw text when the file named a strategy that
/// does not exist: the import still runs with the default and says so in the report.
/// </summary>
public sealed record ImportFile(MatchStrategy Strategy,
                               string? UnrecognizedStrategy,
                               IReadOnlyList<ProductEntryDto?> Entries);
