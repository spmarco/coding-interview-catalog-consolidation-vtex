using System.Text.Json;
using CatalogConsolidation.Application.Exceptions;

namespace CatalogConsolidation.Application;

public static class ProductEntryStreamExtensions
{
    private const string NotAProductArrayMessage = "The file is not a valid JSON array of product entries.";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LenientStringConverter() }
    };

    /// <summary>Reads an uploaded file (same shape as ProductEntry.json) into product entries.</summary>
    /// <exception cref="InvalidImportFileException">The content is not a JSON array of product entries.</exception>
    public static async Task<IReadOnlyList<ProductEntryDto?>> ReadProductEntriesAsync(
        this Stream content,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await JsonSerializer.DeserializeAsync<List<ProductEntryDto?>>(content, Options, cancellationToken);
            return entries ?? throw new InvalidImportFileException(NotAProductArrayMessage);
        }
        catch (JsonException)
        {
            throw new InvalidImportFileException(NotAProductArrayMessage);
        }
    }
}
