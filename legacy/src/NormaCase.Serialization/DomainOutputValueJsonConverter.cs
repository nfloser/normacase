using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Decision;

namespace NormaCase.Serialization;

public sealed class DomainOutputValueJsonConverter : JsonConverter<DomainOutputValue>
{
    public override DomainOutputValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("DomainOutputValue must be an object.");
        }

        var properties = root.EnumerateObject().ToArray();
        if (properties
            .Select(property => property.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() != properties.Length)
        {
            throw new JsonException("Duplicate DomainOutputValue properties are not allowed.");
        }

        if (!root.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("DomainOutputValue kind is required.");
        }

        return kind.GetString() switch
        {
            "UNKNOWN" when properties.Length == 1
                => DomainOutputValue.Unknown,
            "CHOICE" when properties.Length == 2
                && root.TryGetProperty("choice", out var choice)
                && choice.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(choice.GetString())
                && !string.Equals(choice.GetString(), "UNKNOWN", StringComparison.OrdinalIgnoreCase)
                => DomainOutputValue.FromChoice(choice.GetString()!),
            _ => throw new JsonException("Invalid or ambiguous DomainOutputValue.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        DomainOutputValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value.Kind)
        {
            case DomainOutputValueKind.Unknown when value.Choice is null:
                writer.WriteString("kind", "UNKNOWN");
                break;

            case DomainOutputValueKind.Choice
                when !string.IsNullOrWhiteSpace(value.Choice)
                && !string.Equals(value.Choice, "UNKNOWN", StringComparison.OrdinalIgnoreCase):
                writer.WriteString("kind", "CHOICE");
                writer.WriteString("choice", value.Choice);
                break;

            default:
                throw new JsonException("Invalid DomainOutputValue.");
        }

        writer.WriteEndObject();
    }
}
