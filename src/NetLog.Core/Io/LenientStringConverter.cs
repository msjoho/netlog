using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetLog.Core.Io;

/// <summary>Reads a string property that a hand-edited file may hold as a JSON number (e.g. <c>"port": 445</c>)
/// — the text is kept verbatim, null stays null. Writes as a plain string.</summary>
public sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String: return reader.GetString();
            case JsonTokenType.Null: return null;
            case JsonTokenType.Number:
                using (var doc = JsonDocument.ParseValue(ref reader)) return doc.RootElement.GetRawText();
            default: throw new JsonException($"Expected a string or number, got {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}
