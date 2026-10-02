using NormaCase.Application.Assessments;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class AssessmentRecorderTests
{
    [Fact]
    public void Evaluation_captures_explicit_metadata_and_defensively_snapshots_inputs()
    {
        var pack = Load("demo-c");
        var facts = new Dictionary<string, CaseValue>
        {
            ["request_confirmed"] = TruthValue.Yes,
            ["measurement"] = 15m
        };
        var evidence = new Dictionary<string, EvidenceStatus>
        {
            ["verification"] = EvidenceStatus.Present
        };
        var context = new AssessmentExecutionContext(
            "assessment-001",
            "case-001",
            "test-platform-1",
            new DateTimeOffset(2026, 10, 2, 20, 15, 0, TimeSpan.Zero));

        var record = new AssessmentRecorder().Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2),
            evidence,
            context);

        facts["measurement"] = 999m;
        evidence["verification"] = EvidenceStatus.Missing;

        Assert.Equal("assessment-001", record.AssessmentId);
        Assert.Equal("case-001", record.CaseId);
        Assert.Equal("synthetic.demo-c", record.KnowledgePackId);
        Assert.Equal("test-platform-1", record.PlatformVersion);
        Assert.Equal(context.RecordedAtUtc, record.RecordedAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 2), record.Input.AssessmentDate);
        Assert.Equal(CaseValue.FromNumber(15m), record.Input.Facts["measurement"]);
        Assert.Equal(EvidenceStatus.Present, record.Input.Evidence["verification"]);
        Assert.Equal(AssessmentOutcome.Supported, record.Result.Outcome);
        Assert.Equal(record.Input.AssessmentDate, record.Result.AssessmentDate);
        Assert.Equal(pack.Manifest.ReleaseId, record.Result.KnowledgeRelease);
    }

    [Fact]
    public void Snapshot_expands_omitted_declared_inputs_to_explicit_unknown_and_missing()
    {
        var pack = Load("demo-c");
        var facts = new Dictionary<string, CaseValue>
        {
            ["request_confirmed"] = TruthValue.Yes
        };
        var context = new AssessmentExecutionContext(
            "assessment-002",
            "case-002",
            "test-platform-1",
            new DateTimeOffset(2026, 10, 2, 20, 16, 0, TimeSpan.Zero));

        var record = new AssessmentRecorder().Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2),
            evidence: null,
            context);

        Assert.Equal(pack.Fields.Count, record.Input.Facts.Count);
        Assert.Equal(CaseValue.Unknown, record.Input.Facts["measurement"]);
        Assert.Equal(CaseValue.Unknown, record.Input.Facts["alternative_confirmed"]);
        Assert.Equal(EvidenceStatus.Missing, record.Input.Evidence["verification"]);
        Assert.Equal(AssessmentOutcome.HumanReview, record.Result.Outcome);
    }

    [Fact]
    public void Record_rejects_an_input_snapshot_for_a_different_assessment_date()
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
                "assessment-003",
                "case-003",
                "test-platform-1",
                new DateTimeOffset(2026, 10, 2, 20, 17, 0, TimeSpan.Zero)));
        var mismatchedInput = new AssessmentInputSnapshot(
            new DateOnly(2026, 10, 3),
            record.Input.Facts,
            record.Input.Evidence);

        Assert.Throws<ArgumentException>(() => new AssessmentRecord(
            record.AssessmentId,
            record.CaseId,
            record.KnowledgePackId,
            record.PlatformVersion,
            record.RecordedAtUtc,
            mismatchedInput,
            record.Result));
    }

    [Fact]
    public void Invalid_execution_metadata_is_rejected_instead_of_being_invented()
    {
        Assert.Throws<ArgumentException>(() => new AssessmentExecutionContext(
            "",
            "case-003",
            "test-platform-1",
            new DateTimeOffset(2026, 10, 2, 20, 17, 0, TimeSpan.Zero)));

        Assert.Throws<ArgumentException>(() => new AssessmentExecutionContext(
            "assessment-003",
            "case-003",
            "test-platform-1",
            new DateTimeOffset(2026, 10, 2, 22, 17, 0, TimeSpan.FromHours(2))));

        Assert.Throws<ArgumentException>(() => new AssessmentExecutionContext(
            "assessment-003",
            "case-003",
            "test-platform-1",
            default));
    }

    private static NormaCase.Knowledge.Model.KnowledgePack Load(string demo)
        => new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
}
