using System.Text.Json;
using System.Text.Json.Serialization;
using CatalogConsolidation.Domain.Products;

namespace CatalogConsolidation.Application.Entries;

/// <summary>
/// Reads both shapes of the products file: the bare JSON array of entries (the challenge's
/// original format, which keeps the default matching) and the object that also names a strategy.
/// A strategy the enum does not know is kept as raw text instead of failing the whole file, so
/// the import can run with the default and report it.
/// </summary>
public sealed class ImportFileJsonConverter : JsonConverter<ImportFile>
{
    private const string EntriesProperty = "products";
    private const string StrategyProperty = "matchStrategy";

    public override ImportFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return new ImportFile(MatchStrategy.NameAndBrandSimilarity, null, ReadEntries(ref reader, options));
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("The products file must be a JSON array or object.");
        }

        IReadOnlyList<ProductEntryDto?>? entries = null;
        var strategy = MatchStrategy.NameAndBrandSimilarity;
        string? unrecognized = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a property name.");
            }

            var property = reader.GetString();
            reader.Read();

            if (string.Equals(property, EntriesProperty, StringComparison.OrdinalIgnoreCase))
            {
                entries = reader.TokenType == JsonTokenType.Null ? null : ReadEntries(ref reader, options);
            }
            else if (string.Equals(property, StrategyProperty, StringComparison.OrdinalIgnoreCase))
            {
                ReadStrategy(ref reader, ref strategy, ref unrecognized);
            }
            else
            {
                // An unknown property is ignored: a file may carry metadata this import does not use.
                reader.Skip();
            }
        }

        return entries is null
            ? throw new JsonException($"The object has no '{EntriesProperty}' array.")
            : new ImportFile(strategy, unrecognized, entries);
    }

    public override void Write(Utf8JsonWriter writer, ImportFile value, JsonSerializerOptions options)
        => throw new NotSupportedException("An import file is only ever read.");

    private static void ReadStrategy(ref Utf8JsonReader reader, ref MatchStrategy strategy, ref string? unrecognized)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return;
        }

        // Anything that is not a string is reported with its raw text, so the warning can name it.
        var raw = reader.TokenType == JsonTokenType.String
            ? reader.GetString()
            : JsonElement.ParseValue(ref reader).GetRawText();

        // Enum.TryParse also accepts the underlying numbers, including ones with no member.
        if (Enum.TryParse<MatchStrategy>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            strategy = parsed;
        }
        else
        {
            unrecognized = raw;
        }
    }

    private static IReadOnlyList<ProductEntryDto?> ReadEntries(ref Utf8JsonReader reader, JsonSerializerOptions options)
        => JsonSerializer.Deserialize<List<ProductEntryDto?>>(ref reader, options)
           ?? throw new JsonException("The entries must be a JSON array.");
}
