using System.Text.Json;
using System.Text.Json.Serialization;

namespace NormaCase.Serialization;

internal static class InterchangeJson
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();

    internal static T Read<T>(string json) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > AssessmentJson.MaximumJsonCharacters)
            throw new JsonException("JSON exceeds the supported size.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicates(document.RootElement);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Document is required.");
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
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                RejectDuplicates(item);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 64
        };
        options.Converters.Add(new CaseValueJsonConverter());
        options.Converters.Add(new AssessmentIdJsonConverter());
        options.Converters.Add(new CaseIdJsonConverter());
        options.Converters.Add(new ConditionTraceJsonConverter());
        options.Converters.Add(new DomainOutputValueJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        return options;
    }
}
