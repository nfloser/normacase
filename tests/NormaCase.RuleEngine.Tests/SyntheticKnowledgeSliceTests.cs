using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class SyntheticKnowledgeSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Matching_case_returns_supported_with_source_backed_trace()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.Yes,
            ["criterion_c"] = TruthValue.No
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal("demo-a-2026.1", result.KnowledgeRelease);
        Assert.Empty(result.MissingRequiredFields);
        Assert.NotNull(result.RuleTrace);
        Assert.Equal("DEMO-A-ELIGIBILITY", result.RuleTrace.RuleId);
        Assert.Equal(1, result.RuleTrace.RuleVersion);
        Assert.Equal("SYNTH-DEMO-A-001", result.RuleTrace.SourceId);
        Assert.Equal(ConditionResult.Matched, result.RuleTrace.ConditionResult);
        Assert.Equal("all", result.RuleTrace.Condition.Kind);
    }

    [Fact]
    public void Nested_any_branch_can_satisfy_the_rule()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.No,
            ["criterion_c"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(ConditionResult.Matched, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Complete_nonmatching_case_returns_not_supported()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.No,
            ["criterion_b"] = TruthValue.Yes,
            ["criterion_c"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Equal(ConditionResult.NotMatched, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Missing_required_fact_remains_unknown_and_fails_closed()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_b"] = TruthValue.Yes,
            ["criterion_c"] = TruthValue.No
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Equal(["criterion_a"], result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);

        var missingLeaf = result.RuleTrace.Condition.Children[0];
        Assert.Equal(TruthValue.Unknown, missingLeaf.Actual);
        Assert.Equal(ConditionResult.Unknown, missingLeaf.Result);
    }

    [Fact]
    public void Explicit_unknown_required_fact_fails_closed()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.Unknown,
            ["criterion_b"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("criterion_a", result.MissingRequiredFields);
    }

    [Fact]
    public void Assessment_date_is_explicit_and_no_rule_means_human_review()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2025, 12, 31));

        Assert.Equal(new DateOnly(2025, 12, 31), result.AssessmentDate);
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Null(result.RuleTrace);
    }

    [Fact]
    public void Pack_with_missing_source_reference_is_rejected()
    {
        var json = File.ReadAllText(DemoPackPath())
            .Replace(
                ""sourceId": "SYNTH-DEMO-A-001"",
                ""sourceId": "MISSING-SOURCE"",
                StringComparison.Ordinal);

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(json));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "missing_source");
    }

    [Fact]
    public void Case_fields_not_declared_by_the_pack_are_rejected()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, TruthValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.Yes,
            ["undeclared"] = TruthValue.Yes
        };

        Assert.Throws<ArgumentException>(
            () => _evaluator.Evaluate(
                pack,
                facts,
                new DateOnly(2026, 10, 2)));
    }

    private Knowledge.Model.KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static string DemoPackPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-a-pack.json");
}
