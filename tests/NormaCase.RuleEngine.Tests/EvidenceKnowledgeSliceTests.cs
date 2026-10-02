using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class EvidenceKnowledgeSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Complete_evidence_allows_deterministic_supported_result()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_ready"] = TruthValue.Yes
        };
        var evidence = new HashSet<string>(StringComparer.Ordinal)
        {
            "evidence_primary"
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2),
            evidence);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Empty(result.MissingEvidenceRequirements);

        var trace = Assert.IsType<RuleTrace>(result.RuleTrace);
        var evidenceTrace = trace.Condition.Children[1];

        Assert.Equal("evidence_present", evidenceTrace.Kind);
        Assert.Equal("evidence_primary", evidenceTrace.EvidenceId);
        Assert.Equal(ConditionResult.Matched, evidenceTrace.Result);
    }

    [Fact]
    public void Missing_configured_evidence_escalates_to_human_review()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_ready"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Equal(new[] { "evidence_primary" }, result.MissingEvidenceRequirements);

        var trace = Assert.IsType<RuleTrace>(result.RuleTrace);
        var evidenceTrace = trace.Condition.Children[1];

        Assert.Equal(ConditionResult.Unknown, evidenceTrace.Result);
        Assert.Equal(AssessmentOutcome.HumanReview, evidenceTrace.MissingEvidenceOutcome);
    }

    [Fact]
    public void Missing_required_fact_still_fails_as_incomplete_before_evidence_escalation()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>();

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("criterion_ready", result.MissingRequiredFields);
        Assert.Contains("evidence_primary", result.MissingEvidenceRequirements);
    }

    [Fact]
    public void Negative_criterion_does_not_turn_missing_evidence_into_a_positive_result()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_ready"] = TruthValue.No
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Contains("evidence_primary", result.MissingEvidenceRequirements);
    }

    [Fact]
    public void Undeclared_evidence_reference_is_rejected_at_pack_load()
    {
        var json = File.ReadAllText(DemoPackPath())
            .Replace(
                "\"evidenceId\": \"evidence_primary\"",
                "\"evidenceId\": \"missing_evidence\"",
                StringComparison.Ordinal);

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(json));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "missing_evidence_requirement");
    }

    [Fact]
    public void Runtime_evidence_not_declared_by_pack_is_rejected()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_ready"] = TruthValue.Yes
        };
        var evidence = new HashSet<string>(StringComparer.Ordinal)
        {
            "not_declared"
        };

        Assert.Throws<ArgumentException>(
            () => _evaluator.Evaluate(
                pack,
                facts,
                new DateOnly(2026, 10, 2),
                evidence));
    }

    [Fact]
    public void Evidence_requirement_cannot_use_a_positive_missing_outcome()
    {
        var json = File.ReadAllText(DemoPackPath())
            .Replace(
                "\"missingOutcome\": \"HUMAN_REVIEW\"",
                "\"missingOutcome\": \"SUPPORTED\"",
                StringComparison.Ordinal);

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(json));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "invalid_missing_evidence_outcome");
    }

    private KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static string DemoPackPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-c-pack.json");
}
