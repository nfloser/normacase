using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Serialization;

internal sealed class ConditionTraceJsonConverter : JsonConverter<ConditionTrace>
{
    private static readonly HashSet<string> AllowedProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "result",
        "field",
        "expected",
        "actual",
        "minimum",
        "maximum",
        "children",
        "evidenceRequirementId",
        "evidenceStatus",
        "numericExpression"
    };

    public override ConditionTrace Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Condition trace must be an object.");

        foreach (var property in root.EnumerateObject())
        {
            if (!AllowedProperties.Contains(property.Name))
                throw new JsonException($"Unexpected condition trace property '{property.Name}'.");
        }

        var kind = RequiredString(root, "kind");
        var result = Required<ConditionResult>(root, "result", options);
        var field = NullableString(root, "field");
        var expected = Nullable<CaseValue>(root, "expected", options);
        var actual = Nullable<CaseValue>(root, "actual", options);
        var minimum = NullableDecimal(root, "minimum");
        var maximum = NullableDecimal(root, "maximum");
        var children = Required<ConditionTrace[]>(root, "children", options);
        var evidenceRequirementId = OptionalNullableString(root, "evidenceRequirementId");
        var evidenceStatus = OptionalNullable<EvidenceStatus>(root, "evidenceStatus", options);
        var numericExpression = OptionalReference<NumericExpressionTrace>(
            root,
            "numericExpression",
            options);

        return new(
            kind,
            result,
            field,
            expected,
            actual,
            minimum,
            maximum,
            Array.AsReadOnly(children),
            evidenceRequirementId,
            evidenceStatus,
            numericExpression);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ConditionTrace value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", value.Kind);
        writer.WritePropertyName("result");
        JsonSerializer.Serialize(writer, value.Result, options);
        WriteNullableString(writer, "field", value.Field);
        writer.WritePropertyName("expected");
        JsonSerializer.Serialize(writer, value.Expected, options);
        writer.WritePropertyName("actual");
        JsonSerializer.Serialize(writer, value.Actual, options);
        WriteNullableDecimal(writer, "minimum", value.Minimum);
        WriteNullableDecimal(writer, "maximum", value.Maximum);
        writer.WritePropertyName("children");
        JsonSerializer.Serialize(writer, value.Children, options);
        WriteNullableString(writer, "evidenceRequirementId", value.EvidenceRequirementId);
        writer.WritePropertyName("evidenceStatus");
        JsonSerializer.Serialize(writer, value.EvidenceStatus, options);
        writer.WritePropertyName("numericExpression");
        JsonSerializer.Serialize(writer, value.NumericExpression, options);
        writer.WriteEndObject();
    }

    private static T Required<T>(
        JsonElement root,
        string name,
        JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(name, out var value))
            throw new JsonException($"Condition trace property '{name}' is required.");

        return JsonSerializer.Deserialize<T>(value.GetRawText(), options)
            ?? throw new JsonException($"Condition trace property '{name}' is required.");
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new JsonException($"Condition trace property '{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static string? NullableString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            throw new JsonException($"Condition trace property '{name}' is required.");

        return ReadNullableString(value, name);
    }

    private static string? OptionalNullableString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value)
            ? ReadNullableString(value, name)
            : null;

    private static string? ReadNullableString(JsonElement value, string name)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new JsonException($"Condition trace property '{name}' must be a string or null.");
        return value.GetString();
    }

    private static T? Nullable<T>(
        JsonElement root,
        string name,
        JsonSerializerOptions options)
        where T : struct
    {
        if (!root.TryGetProperty(name, out var value))
            throw new JsonException($"Condition trace property '{name}' is required.");
        if (value.ValueKind == JsonValueKind.Null)
            return null;

        return JsonSerializer.Deserialize<T>(value.GetRawText(), options);
    }

    private static T? OptionalNullable<T>(
        JsonElement root,
        string name,
        JsonSerializerOptions options)
        where T : struct
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(value.GetRawText(), options);
    }

    private static T? OptionalReference<T>(
        JsonElement root,
        string name,
        JsonSerializerOptions options)
        where T : class
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(value.GetRawText(), options)
            ?? throw new JsonException($"Condition trace property '{name}' is invalid.");
    }

    private static decimal? NullableDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
            throw new JsonException($"Condition trace property '{name}' is required.");
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
            throw new JsonException($"Condition trace property '{name}' must be a decimal number or null.");
        return number;
    }

    private static void WriteNullableString(
        Utf8JsonWriter writer,
        string name,
        string? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteString(name, value);
    }

    private static void WriteNullableDecimal(
        Utf8JsonWriter writer,
        string name,
        decimal? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteNumber(name, value.Value);
    }
}
