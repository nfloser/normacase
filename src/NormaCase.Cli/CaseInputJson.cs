using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;
using NormaCase.Serialization;

namespace NormaCase.Cli;

internal sealed record CaseInputDocument(
    int FormatVersion,
    Dictionary<string, CaseValue> Facts,
    Dictionary<string, EvidenceStatus>? Evidence);

internal static class CaseInputJson
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumJsonCharacters = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static CaseInputDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > MaximumJsonCharacters)
            throw new JsonException("Case input exceeds the supported size.");

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicates(document.RootElement);

        var input = JsonSerializer.Deserialize<CaseInputDocument>(json, Options)
            ?? throw new JsonException("Case input is required.");

        if (input.FormatVersion != CurrentFormatVersion)
            throw new JsonException("Unsupported case input format.");
        if (input.Facts is null)
            throw new JsonException("Facts are required.");

        return input with
        {
            Facts = new Dictionary<string, CaseValue>(input.Facts, StringComparer.Ordinal),
            Evidence = input.Evidence is null
                ? new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal)
                : new Dictionary<string, EvidenceStatus>(input.Evidence, StringComparer.Ordinal)
        };
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException("Duplicate JSON properties are not allowed.");
                RejectDuplicates(property.Value);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in element.EnumerateArray())
            RejectDuplicates(item);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 64
        };

        options.Converters.Add(new CaseValueJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        return options;
    }
}
