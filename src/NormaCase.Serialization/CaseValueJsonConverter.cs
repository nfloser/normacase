using System.Text.Json;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;

namespace NormaCase.Serialization;

public sealed class CaseValueJsonConverter : JsonConverter<CaseValue>
{
    public override CaseValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("CaseValue must be an object.");

        var properties = root.EnumerateObject().ToArray();
        if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
            throw new JsonException("Duplicate CaseValue properties are not allowed.");
        if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
            throw new JsonException("CaseValue kind is required.");

        switch (kind.GetString())
        {
            case "UNKNOWN" when properties.Length == 1:
                return CaseValue.Unknown;
            case "NUMBER" when properties.Length == 2
                && root.TryGetProperty("number", out var number)
                && number.ValueKind == JsonValueKind.Number
                && number.TryGetDecimal(out var value):
                if (!IsExactDecimal(number.GetRawText(), value))
                    throw new JsonException("Number cannot be represented exactly as decimal.");
                return CaseValue.FromNumber(value);
            case "TRUTH" when properties.Length == 2
                && root.TryGetProperty("truth", out var truth)
                && truth.ValueKind == JsonValueKind.String:
                return truth.GetString() switch
                {
                    "YES" => CaseValue.FromTruth(TruthValue.Yes),
                    "NO" => CaseValue.FromTruth(TruthValue.No),
                    "NOT_APPLICABLE" => CaseValue.FromTruth(TruthValue.NotApplicable),
                    _ => throw new JsonException("Invalid truth value; UNKNOWN uses its own value kind.")
                };
            default:
                throw new JsonException("Invalid or ambiguous CaseValue.");
        }
    }

    private static bool IsExactDecimal(string token, decimal value)
    {
        var exponentIndex = token.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? token : token[..exponentIndex];
        var exponent = 0;
        if (exponentIndex >= 0
            && !int.TryParse(token[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            return false;
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

    public override void Write(Utf8JsonWriter writer, CaseValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value.Kind)
        {
            case CaseValueKind.Unknown when value.Truth is null && value.Number is null:
                writer.WriteString("kind", "UNKNOWN");
                break;
            case CaseValueKind.Number when value.Number is not null && value.Truth is null:
                writer.WriteString("kind", "NUMBER");
                writer.WriteNumber("number", value.Number.Value);
                break;
            case CaseValueKind.Truth when value.Number is null:
                var truth = value.Truth switch
                {
                    TruthValue.Yes => "YES",
                    TruthValue.No => "NO",
                    TruthValue.NotApplicable => "NOT_APPLICABLE",
                    _ => throw new JsonException("Invalid truth value.")
                };
                writer.WriteString("kind", "TRUTH");
                writer.WriteString("truth", truth);
                break;
            default:
                throw new JsonException("Invalid CaseValue.");
        }
        writer.WriteEndObject();
    }
}
