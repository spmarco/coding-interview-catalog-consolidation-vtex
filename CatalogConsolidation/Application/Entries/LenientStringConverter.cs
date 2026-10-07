using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogConsolidation.Application.Entries;

/// <summary>
/// Reads any JSON scalar as text, so a row whose field is a number or boolean (for example a
/// numeric seller Id) is processed or rejected on its own, instead of failing the whole file.
/// Objects and arrays carry no usable text and are read as null, which the importer then
/// rejects if the field is required.
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
                return JsonElement.ParseValue(ref reader).GetRawText();
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
