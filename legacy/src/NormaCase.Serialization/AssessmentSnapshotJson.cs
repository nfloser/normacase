using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NormaCase.Serialization;

/// <summary>Portable replay data. Checksums detect changes but do not authenticate or approve content.</summary>
public sealed record AssessmentSnapshot(
    int FormatVersion,
    string KnowledgePackJson,
    string KnowledgePackSha256,
    CaseInput Input,
    AssessmentDocument Assessment,
    string ContentSha256);

public static class AssessmentSnapshotJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(
        string knowledgePackJson,
        CaseInput input,
        AssessmentDocument assessment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackJson);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(assessment);

        var knowledgePackSha256 = HashText(knowledgePackJson);
        var payload = new Payload(
            CurrentFormatVersion,
            knowledgePackJson,
            knowledgePackSha256,
            input,
            assessment);
        var snapshot = new AssessmentSnapshot(
            payload.FormatVersion,
            knowledgePackJson,
            knowledgePackSha256,
            input,
            assessment,
            HashPayload(payload));

        var json = JsonSerializer.Serialize(
            snapshot,
            InterchangeJson.Options);

        // Apply the same strict transport checks to captured and restored data.
        Deserialize(json);
        return json;
    }

    public static AssessmentSnapshot Deserialize(string json)
    {
        var snapshot = InterchangeJson.Read<AssessmentSnapshot>(json);

        if (snapshot.FormatVersion != CurrentFormatVersion
            || snapshot.Assessment.FormatVersion
                != AssessmentJson.CurrentFormatVersion
            || string.IsNullOrWhiteSpace(snapshot.KnowledgePackJson)
            || snapshot.KnowledgePackJson.Length
                > AssessmentJson.MaximumJsonCharacters
            || snapshot.Input.AssessmentDate
                != snapshot.Assessment.Assessment.AssessmentDate)
        {
            throw new JsonException("Invalid snapshot envelope.");
        }

        CaseInputJson.Deserialize(
            JsonSerializer.Serialize(
                snapshot.Input,
                InterchangeJson.Options));
        AssessmentJson.Deserialize(
            JsonSerializer.Serialize(
                snapshot.Assessment,
                InterchangeJson.Options));

        RequireHash(
            snapshot.KnowledgePackSha256,
            HashText(snapshot.KnowledgePackJson),
            "Knowledge Pack checksum mismatch.");

        var payload = new Payload(
            snapshot.FormatVersion,
            snapshot.KnowledgePackJson,
            snapshot.KnowledgePackSha256,
            snapshot.Input,
            snapshot.Assessment);
        RequireHash(
            snapshot.ContentSha256,
            HashPayload(payload),
            "Snapshot checksum mismatch.");

        return snapshot;
    }

    private static string HashText(string value)
        => Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));

    private static string HashPayload(Payload payload)
        => HashText(
            JsonSerializer.Serialize(
                payload,
                InterchangeJson.Options));

    private static void RequireHash(
        string actual,
        string expected,
        string error)
    {
        if (actual.Length != 64
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(actual)))
        {
            throw new JsonException(error);
        }
    }

    private sealed record Payload(
        int FormatVersion,
        string KnowledgePackJson,
        string KnowledgePackSha256,
        CaseInput Input,
        AssessmentDocument Assessment);
}
