using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class PitchDemoScenarioTests
{
    private static readonly DateOnly AssessmentDate = new(2026, 10, 3);
    private readonly RuleEvaluator _evaluator = new();
    private readonly KnowledgePackLoader _loader = new();

    [Fact]
    public void Missing_required_input_stays_incomplete()
    {
        var result = Evaluate(
            new Dictionary<string, CaseValue>
            {
                ["criteria_confirmed"] = TruthValue.Yes
            },
            EvidenceStatus.Present);

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Equal(new[] { "request_complete" }, result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
        AssertTraceIdentity(result.RuleTrace);
    }

    [Fact]
    public void Missing_evidence_routes_to_human_review()
    {
        var result = Evaluate(
            CompletePositiveFacts(),
            EvidenceStatus.Missing);

        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
        AssertTraceIdentity(result.RuleTrace);
    }

    [Fact]
    public void Complete_positive_case_is_supported_and_source_bound()
    {
        var result = Evaluate(
            CompletePositiveFacts(),
            EvidenceStatus.Present);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Equal("demo-g-2026.1", result.KnowledgeRelease);
        Assert.Equal(ConditionResult.Matched, result.RuleTrace!.ConditionResult);
        AssertTraceIdentity(result.RuleTrace);
        Assert.Equal(
            "repository:knowledge/demo-g/pack.json",
            result.RuleTrace.Source.SourceLocation);
    }

    [Fact]
    public void Explicit_negative_input_is_not_supported()
    {
        var result = Evaluate(
            new Dictionary<string, CaseValue>
            {
                ["request_complete"] = TruthValue.Yes,
                ["criteria_confirmed"] = TruthValue.No
            },
            EvidenceStatus.Present);

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Equal(ConditionResult.NotMatched, result.RuleTrace!.ConditionResult);
        AssertTraceIdentity(result.RuleTrace);
    }

    private AssessmentResult Evaluate(
        IReadOnlyDictionary<string, CaseValue> facts,
        EvidenceStatus evidenceStatus)
    {
        var pack = _loader.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-g-pack.json"));

        return _evaluator.Evaluate(
            pack,
            facts,
            AssessmentDate,
            new Dictionary<string, EvidenceStatus>
            {
                ["supporting_document"] = evidenceStatus
            });
    }

    private static Dictionary<string, CaseValue> CompletePositiveFacts()
        => new(StringComparer.Ordinal)
        {
            ["request_complete"] = TruthValue.Yes,
            ["criteria_confirmed"] = TruthValue.Yes
        };

    private static void AssertTraceIdentity(RuleTrace trace)
    {
        Assert.Equal("DEMO-G-DECISION", trace.RuleId);
        Assert.Equal(1, trace.RuleVersion);
        Assert.Equal("SYNTH-DEMO-G-001", trace.SourceId);
        Assert.Equal("SYNTH-DEMO-G-001", trace.Source.Id);
        Assert.Equal("NormaCase synthetic pitch fixture", trace.Source.Authority);
        Assert.Equal("SYNTHETIC", trace.Source.DocumentType);
    }
}
