using System.Text.Json;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;

namespace NormaCase.Replay;

/// <summary>Captures explicit inputs and verifies replay without active knowledge lookup or implicit time.</summary>
public sealed class AssessmentSnapshotService
{
    public string Capture(string knowledgePackJson, string caseJson, string platformVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackJson);
        if (knowledgePackJson.Length > AssessmentJson.MaximumJsonCharacters)
            throw new JsonException("Knowledge input exceeds supported size.");
        var pack = new KnowledgePackLoader().LoadFromJson(knowledgePackJson);
        var input = CaseInputJson.Deserialize(caseJson);
        var result = new RuleEvaluator().Evaluate(pack, input.Facts, input.AssessmentDate, input.Evidence);
        return AssessmentSnapshotJson.Serialize(knowledgePackJson, input,
            new AssessmentDocument(AssessmentJson.CurrentFormatVersion, platformVersion, result));
    }

    public AssessmentDocument Replay(string snapshotJson, string currentPlatformVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPlatformVersion);
        var snapshot = AssessmentSnapshotJson.Deserialize(snapshotJson);
        if (!string.Equals(currentPlatformVersion, snapshot.Assessment.PlatformVersion, StringComparison.Ordinal))
            throw new SnapshotReplayException();
        var pack = new KnowledgePackLoader().LoadFromJson(snapshot.KnowledgePackJson);
        if (!string.Equals(pack.Manifest.ReleaseId, snapshot.Assessment.Assessment.KnowledgeRelease, StringComparison.Ordinal))
            throw new SnapshotReplayException();
        var result = new RuleEvaluator().Evaluate(pack, snapshot.Input.Facts,
            snapshot.Input.AssessmentDate, snapshot.Input.Evidence);
        if (!string.Equals(AssessmentJson.Serialize(result, currentPlatformVersion),
            AssessmentJson.Serialize(snapshot.Assessment.Assessment, currentPlatformVersion), StringComparison.Ordinal))
            throw new SnapshotReplayException();
        return new AssessmentDocument(AssessmentJson.CurrentFormatVersion, currentPlatformVersion, result);
    }
}

public sealed class SnapshotReplayException : Exception
{
    public SnapshotReplayException() : base("Snapshot replay verification failed.") { }
}
