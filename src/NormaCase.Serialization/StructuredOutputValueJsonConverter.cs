using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Decision;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Serialization;

public sealed class StructuredOutputValueJsonConverter : JsonConverter<StructuredOutputValue>
{
    public override StructuredOutputValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("StructuredOutputValue must be an object.");

        var properties = root.EnumerateObject().ToArray();
        if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
            throw new JsonException("Duplicate StructuredOutputValue properties are not allowed.");
        if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
            throw new JsonException("StructuredOutputValue kind is required.");

        switch (kind.GetString())
        {
            case "UNKNOWN" when properties.Length == 1:
                return StructuredOutputValue.Unknown;

            case "TRUTH" when properties.Length == 2
                && root.TryGetProperty("truth", out var truth)
                && truth.ValueKind == JsonValueKind.String:
                return truth.GetString() switch
                {
                    "YES" => StructuredOutputValue.FromTruth(TruthValue.Yes),
                    "NO" => StructuredOutputValue.FromTruth(TruthValue.No),
                    "NOT_APPLICABLE" => StructuredOutputValue.FromTruth(TruthValue.NotApplicable),
                    _ => throw new JsonException("Invalid structured truth value.")
                };

            case "NUMBER" when properties.Length == 2
                && root.TryGetProperty("number", out var number)
                && number.ValueKind == JsonValueKind.Number
                && number.TryGetDecimal(out var value):
                if (!IsExactDecimal(number.GetRawText(), value))
                    throw new JsonException("Number cannot be represented exactly as decimal.");
                return StructuredOutputValue.FromNumber(value);

            case "CODE" when properties.Length == 2
                && root.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(code.GetString()):
                return StructuredOutputValue.FromCode(code.GetString()!);

            default:
                throw new JsonException("Invalid or ambiguous StructuredOutputValue.");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        StructuredOutputValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value.Kind)
        {
            case StructuredOutputValueKind.Unknown
                when value.Truth is null && value.Number is null && value.Code is null:
                writer.WriteString("kind", "UNKNOWN");
                break;

            case StructuredOutputValueKind.Truth
                when value.Truth is not null && value.Number is null && value.Code is null:
                writer.WriteString("kind", "TRUTH");
                writer.WriteString("truth", value.Truth switch
                {
                    TruthValue.Yes => "YES",
                    TruthValue.No => "NO",
                    TruthValue.NotApplicable => "NOT_APPLICABLE",
                    _ => throw new JsonException("Invalid structured truth value.")
                });
                break;

            case StructuredOutputValueKind.Number
                when value.Number is not null && value.Truth is null && value.Code is null:
                writer.WriteString("kind", "NUMBER");
                writer.WriteNumber("number", value.Number.Value);
                break;

            case StructuredOutputValueKind.Code
                when value.Code is not null && value.Truth is null && value.Number is null:
                writer.WriteString("kind", "CODE");
                writer.WriteString("code", value.Code);
                break;

            default:
                throw new JsonException("Invalid StructuredOutputValue.");
        }

        writer.WriteEndObject();
    }

    private static bool IsExactDecimal(string token, decimal value)
    {
        var exponentIndex = token.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? token : token[..exponentIndex];
        var exponent = 0;
        if (exponentIndex >= 0
            && !int.TryParse(
                token[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out exponent))
        {
            return false;
        }

        var point = mantissa.IndexOf('.');
        var fractionalDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var coefficient = BigInteger.Parse(mantissa.Replace(".", ""), CultureInfo.InvariantCulture);
        if (coefficient.IsZero)
            return value == 0m;

        var bits = decimal.GetBits(value);
        var decimalCoefficient = (BigInteger)(uint)bits[0]
            | ((BigInteger)(uint)bits[1] << 32)
            | ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0)
            decimalCoefficient = -decimalCoefficient;

        var decimalScale = (bits[3] >> 16) & 0xFF;
        var power = (long)exponent - fractionalDigits + decimalScale;
        if (power is < -1000 or > 1000)
            return false;

        return power >= 0
            ? coefficient * BigInteger.Pow(10, (int)power) == decimalCoefficient
            : coefficient == decimalCoefficient * BigInteger.Pow(10, (int)-power);
    }
}
