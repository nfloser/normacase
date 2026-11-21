using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Audit;

namespace NormaCase.Serialization;

internal sealed class AssessmentIdJsonConverter : JsonConverter<AssessmentId>
{
    public override AssessmentId Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Assessment id must be a string.");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("Assessment id must not be blank.");

        return new AssessmentId(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        AssessmentId value,
        JsonSerializerOptions options)
    {
        if (value.IsEmpty)
            throw new JsonException("Assessment id must be explicit.");

        writer.WriteStringValue(value.Value);
    }
}
