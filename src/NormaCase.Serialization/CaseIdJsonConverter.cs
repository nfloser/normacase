using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Cases;

namespace NormaCase.Serialization;

internal sealed class CaseIdJsonConverter
    : JsonConverter<CaseId>
{
    public override CaseId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Case id must be a string.");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("Case id must not be blank.");

        return new CaseId(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        CaseId value,
        JsonSerializerOptions options)
    {
        if (value.IsEmpty)
            throw new JsonException("Case id must be explicit.");

        writer.WriteStringValue(value.Value);
    }
}
