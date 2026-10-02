using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class NumericKnowledgeSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Version_one_accepts_inclusive_lower_boundaries()
    {
        var result = Evaluate(
            score: 10m,
            bandValue: 18m,
            assessmentDate: new DateOnly(2026, 6, 30));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        var trace = Assert.IsType<RuleTrace>(result.RuleTrace);
        Assert.Equal(1, trace.RuleVersion);
        Assert.Equal("SYNTH-DEMO-B-001", trace.SourceId);

        var scoreTrace = trace.Condition.Children[0];
        Assert.Equal(CaseValueKind.Number, scoreTrace.Actual!.Value.Kind);
        Assert.Equal(10m, scoreTrace.Actual.Value.Number);
        Assert.Equal(10m, scoreTrace.Expected!.Value.Number);
    }

    [Fact]
    public void Numeric_threshold_rejects_value_below_boundary()
    {
        var result = Evaluate(
            score: 9.99m,
            bandValue: 30m,
            assessmentDate: new DateOnly(2026, 6, 30));

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Equal(ConditionResult.NotMatched, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Inclusive_range_accepts_upper_boundary()
    {
        var result = Evaluate(
            score: 10m,
            bandValue: 65m,
            assessmentDate: new DateOnly(2026, 6, 30));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
    }

    [Fact]
    public void Temporal_boundary_selects_version_two()
    {
        var result = Evaluate(
            score: 10m,
            bandValue: 30m,
            assessmentDate: new DateOnly(2026, 7, 1));

        Assert.Equal(AssessmentOutcome.NotSupported, result.Outcome);
        Assert.Equal(2, result.RuleTrace!.RuleVersion);
    }

    [Fact]
    public void Version_two_accepts_its_new_boundaries()
    {
        var result = Evaluate(
            score: 12m,
            bandValue: 20m,
            assessmentDate: new DateOnly(2026, 7, 1));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(2, result.RuleTrace!.RuleVersion);
    }

    [Fact]
    public void Missing_required_numeric_value_is_incomplete()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["band_value"] = 30m
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("score", result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Truth_value_cannot_be_coerced_into_numeric_field()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["score"] = TruthValue.Yes,
            ["band_value"] = 30m
        };

        Assert.Throws<ArgumentException>(
            () => _evaluator.Evaluate(
                pack,
                facts,
                new DateOnly(2026, 10, 2)));
    }

    private AssessmentResult Evaluate(
        decimal score,
        decimal bandValue,
        DateOnly assessmentDate)
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["score"] = score,
            ["band_value"] = bandValue
        };

        return _evaluator.Evaluate(pack, facts, assessmentDate);
    }

    private KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static string DemoPackPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-b-pack.json");
}
