using System.Text.Json;
using CatalogConsolidation.Application.Exceptions;

namespace CatalogConsolidation.Application.Entries;

public static class ImportFileStreamExtensions
{
    private const string NotAProductFileMessage =
        "The file is not a valid products file: expected a JSON array of product entries, or an object with a 'products' array.";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LenientStringConverter(), new ImportFileJsonConverter() }
    };

    /// <summary>Reads an uploaded file (see README "Input") into its entries and the matching rule it asks for.</summary>
    /// <exception cref="InvalidImportFileException">The content is not a products file.</exception>
    public static async Task<ImportFile> ReadImportFileAsync(this Stream content, CancellationToken cancellationToken)
    {
        try
        {
            var file = await JsonSerializer.DeserializeAsync<ImportFile>(content, Options, cancellationToken);
            return file ?? throw new InvalidImportFileException(NotAProductFileMessage);
        }
        catch (JsonException)
        {
            throw new InvalidImportFileException(NotAProductFileMessage);
        }
    }
}
