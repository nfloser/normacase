using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Serialization;

/// <summary>An explicit interchange format, not an approval, signature or persistence store.</summary>
public sealed record AssessmentDocument(int FormatVersion, string PlatformVersion, AssessmentResult Assessment);

public static class AssessmentJson
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumJsonCharacters = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(AssessmentResult assessment, string platformVersion)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        return JsonSerializer.Serialize(new AssessmentDocument(CurrentFormatVersion, platformVersion, assessment), Options);
    }

    public static AssessmentDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > MaximumJsonCharacters)
            throw new JsonException("Assessment JSON exceeds the supported size.");

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
        RejectDuplicates(document.RootElement);
        var restored = JsonSerializer.Deserialize<AssessmentDocument>(json, Options)
            ?? throw new JsonException("Assessment document is required.");
        if (restored.FormatVersion != CurrentFormatVersion)
            throw new JsonException("Unsupported assessment document format.");
        if (string.IsNullOrWhiteSpace(restored.PlatformVersion)
            || string.IsNullOrWhiteSpace(restored.Assessment.KnowledgeRelease))
            throw new JsonException("Platform and Knowledge Release versions are required.");
        return restored;
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
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        return options;
    }
}
