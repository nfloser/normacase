using System.Text.Json;
using NormaCase.Application.Assessments;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class AssessmentRecordJsonTests
{
    [Fact]
    public void Full_record_roundtrips_without_losing_decimal_unknown_or_source_revision()
    {
        var pack = Load("demo-b");
        var record = new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["score"] = CaseValue.FromNumber(123456789.1234567890123456789m)
            },
            new DateOnly(2026, 10, 2),
            evidence: null,
            new AssessmentExecutionContext(
                "assessment-json-001",
                "case-json-001",
                "test-platform-2",
                new DateTimeOffset(2026, 10, 2, 20, 30, 0, TimeSpan.Zero)));

        var json = AssessmentRecordJson.Serialize(record);
        var restored = AssessmentRecordJson.Deserialize(json);

        Assert.Equal(json, AssessmentRecordJson.Serialize(restored));
        Assert.Equal(
            CaseValue.FromNumber(123456789.1234567890123456789m),
            restored.Input.Facts["score"]);
        Assert.Equal(CaseValue.Unknown, restored.Input.Facts["band_value"]);
        Assert.Equal(record.Result.RuleTrace!.Source, restored.Result.RuleTrace!.Source);
        Assert.Equal("synthetic.demo-b", restored.KnowledgePackId);
        Assert.Equal("test-platform-2", restored.PlatformVersion);
    }

    [Fact]
    public void Evidence_states_and_human_review_roundtrip()
    {
        var pack = Load("demo-c");
        var record = new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = TruthValue.Yes,
                ["measurement"] = 15m
            },
            new DateOnly(2026, 10, 2),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = EvidenceStatus.Conflicting
            },
            new AssessmentExecutionContext(
                "assessment-json-002",
                "case-json-002",
                "test-platform-2",
                new DateTimeOffset(2026, 10, 2, 20, 31, 0, TimeSpan.Zero)));

        var restored = AssessmentRecordJson.Deserialize(
            AssessmentRecordJson.Serialize(record));

        Assert.Equal(EvidenceStatus.Conflicting, restored.Input.Evidence["verification"]);
        Assert.Equal(AssessmentOutcome.HumanReview, restored.Result.Outcome);
        Assert.Equal(
            EvidenceStatus.Conflicting,
            restored.Result.RuleTrace!.Condition.EvidenceStatus);
    }

    [Fact]
    public void Unsupported_or_ambiguous_record_documents_are_rejected()
    {
        var record = new AssessmentRecorder().Evaluate(
            Load("demo-a"),
            new Dictionary<string, CaseValue>
            {
                ["criterion_a"] = TruthValue.Yes,
                ["criterion_b"] = TruthValue.Yes
            },
            new DateOnly(2026, 10, 2),
            evidence: null,
            new AssessmentExecutionContext(
                "assessment-json-003",
                "case-json-003",
                "test-platform-2",
                new DateTimeOffset(2026, 10, 2, 20, 32, 0, TimeSpan.Zero)));

        var json = AssessmentRecordJson.Serialize(record);

        Assert.Throws<JsonException>(() => AssessmentRecordJson.Deserialize(
            json.Replace("\"formatVersion\":1", "\"formatVersion\":2")));
        Assert.Throws<JsonException>(() => AssessmentRecordJson.Deserialize(
            json.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")));
    }

    private static NormaCase.Knowledge.Model.KnowledgePack Load(string demo)
        => new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
}
