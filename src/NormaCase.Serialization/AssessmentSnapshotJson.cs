using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NormaCase.Serialization;

/// <summary>Portable replay data. The checksum is not an authentication or approval mechanism.</summary>
public sealed record AssessmentSnapshot(
    int FormatVersion, string KnowledgePackJson, CaseInput Input,
    AssessmentDocument Assessment, string ContentSha256);

public static class AssessmentSnapshotJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(string knowledgePackJson, CaseInput input, AssessmentDocument assessment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackJson);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(assessment);
        var payload = new Payload(CurrentFormatVersion, knowledgePackJson, input, assessment);
        var snapshot = new AssessmentSnapshot(payload.FormatVersion, knowledgePackJson, input, assessment, Hash(payload));
        var json = JsonSerializer.Serialize(snapshot, InterchangeJson.Options);
        // Apply the same strict transport checks to captured and restored data.
        Deserialize(json);
        return json;
    }

    public static AssessmentSnapshot Deserialize(string json)
    {
        var snapshot = InterchangeJson.Read<AssessmentSnapshot>(json);
        if (snapshot.FormatVersion != CurrentFormatVersion
            || snapshot.Assessment.FormatVersion != AssessmentJson.CurrentFormatVersion
            || string.IsNullOrWhiteSpace(snapshot.KnowledgePackJson)
            || snapshot.KnowledgePackJson.Length > AssessmentJson.MaximumJsonCharacters
            || snapshot.Input.AssessmentDate != snapshot.Assessment.Assessment.AssessmentDate)
            throw new JsonException("Invalid snapshot envelope.");
        CaseInputJson.Deserialize(JsonSerializer.Serialize(snapshot.Input, InterchangeJson.Options));
        AssessmentJson.Deserialize(JsonSerializer.Serialize(snapshot.Assessment, InterchangeJson.Options));
        var expected = Hash(new Payload(snapshot.FormatVersion, snapshot.KnowledgePackJson, snapshot.Input, snapshot.Assessment));
        if (snapshot.ContentSha256.Length != 64
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(snapshot.ContentSha256)))
            throw new JsonException("Snapshot checksum mismatch.");
        return snapshot;
    }

    private static string Hash(Payload payload) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, InterchangeJson.Options))));

    private sealed record Payload(int FormatVersion, string KnowledgePackJson, CaseInput Input, AssessmentDocument Assessment);
}
