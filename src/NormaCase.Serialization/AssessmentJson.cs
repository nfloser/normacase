using System.Text.Json;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Serialization;

/// <summary>An explicit interchange format, not an approval, signature or persistence store.</summary>
public sealed record AssessmentDocument(int FormatVersion, string PlatformVersion, AssessmentResult Assessment);

public static class AssessmentJson
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumJsonCharacters = 8 * 1024 * 1024;

    public static string Serialize(AssessmentResult assessment, string platformVersion)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        return JsonSerializer.Serialize(new AssessmentDocument(CurrentFormatVersion, platformVersion, assessment), InterchangeJson.Options);
    }

    public static AssessmentDocument Deserialize(string json)
    {
        var restored = InterchangeJson.Read<AssessmentDocument>(json);
        if (restored.FormatVersion != CurrentFormatVersion)
            throw new JsonException("Unsupported assessment document format.");
        if (string.IsNullOrWhiteSpace(restored.PlatformVersion)
            || string.IsNullOrWhiteSpace(restored.Assessment.KnowledgeRelease))
            throw new JsonException("Platform and Knowledge Release versions are required.");
        return restored;
    }
}
