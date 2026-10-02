using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class CalculationKnowledgeSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Ordered_calculations_feed_later_calculations_and_the_rule()
    {
        var result = Evaluate(
            baseValue: 10m,
            comparisonValue: 4m,
            bandInput: 20m);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(3, result.Calculations.Count);

        AssertCalculation(result.Calculations[0], "band_score", "range_lookup", 5m);
        AssertCalculation(result.Calculations[1], "combined_total", "sum", 15m);
        AssertCalculation(result.Calculations[2], "peak_value", "max", 5m);

        var range = Assert.IsType<RangeLookupTrace>(result.Calculations[0].SelectedRange);
        Assert.Equal(20m, range.Minimum);
        Assert.True(range.MinimumInclusive);
        Assert.Equal(100m, range.Maximum);
        Assert.True(range.MaximumInclusive);
        Assert.Equal(5m, range.Value);
    }

    [Fact]
    public void Half_open_range_boundary_selects_exactly_one_band()
    {
        var result = Evaluate(
            baseValue: 10m,
            comparisonValue: 5m,
            bandInput: 10m);

        var range = Assert.IsType<RangeLookupTrace>(result.Calculations[0].SelectedRange);

        Assert.Equal(10m, range.Minimum);
        Assert.Equal(20m, range.Maximum);
        Assert.Equal(2.5m, range.Value);
    }

    [Fact]
    public void Lookup_input_outside_declared_coverage_fails_closed()
    {
        var result = Evaluate(
            baseValue: 10m,
            comparisonValue: 5m,
            bandInput: 101m);

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);

        var band = result.Calculations.Single(item => item.Id == "band_score");
        Assert.True(band.Result.IsUnknown);
        Assert.Null(band.SelectedRange);

        var total = result.Calculations.Single(item => item.Id == "combined_total");
        Assert.True(total.Result.IsUnknown);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Unknown_optional_input_propagates_through_max_and_rule()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_value"] = 10m,
            ["band_input"] = 10m
        };

        var result = _evaluator.Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Empty(result.MissingRequiredFields);

        var peak = result.Calculations.Single(item => item.Id == "peak_value");
        Assert.True(peak.Result.IsUnknown);
        Assert.Contains(peak.Inputs, input => input.Id == "comparison_value" && input.Value.IsUnknown);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
    }

    [Fact]
    public void Caller_cannot_override_a_derived_field()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_value"] = 10m,
            ["comparison_value"] = 5m,
            ["band_input"] = 20m,
            ["combined_total"] = 999m
        };

        Assert.Throws<ArgumentException>(
            () => _evaluator.Evaluate(
                pack,
                facts,
                new DateOnly(2026, 10, 2)));
    }

    [Fact]
    public void Forward_calculation_reference_is_rejected()
    {
        var node = DemoD();
        node["calculations"]![0]!["input"] = "combined_total";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "calculation_forward_reference");
    }

    [Fact]
    public void Cyclic_calculation_dependencies_are_rejected()
    {
        var node = DemoD();
        node["calculations"]![0]!["input"] = "combined_total";
        node["calculations"]![1]!["inputs"]![0] = "band_score";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "calculation_forward_reference");
    }

    [Fact]
    public void Calculation_requires_numeric_inputs()
    {
        var node = DemoD();
        node["fields"]![0]!["type"] = "truth";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "calculation_input_type_mismatch");
    }

    [Fact]
    public void Overlapping_lookup_ranges_are_rejected()
    {
        var node = DemoD();
        node["calculations"]![0]!["ranges"]![1]!["minimum"] = 9;

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "overlapping_lookup_ranges");
    }

    [Fact]
    public void Full_coverage_rejects_a_gap_between_ranges()
    {
        var node = DemoD();
        node["calculations"]![0]!["ranges"]![1]!["minimum"] = 11;

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "lookup_coverage_gap");
    }

    [Fact]
    public void Full_coverage_rejects_uncovered_declared_endpoint()
    {
        var node = DemoD();
        node["calculations"]![0]!["ranges"]![2]!["maximum"] = 99;

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "lookup_coverage_boundary");
    }

    private AssessmentResult Evaluate(
        decimal baseValue,
        decimal comparisonValue,
        decimal bandInput)
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_value"] = baseValue,
            ["comparison_value"] = comparisonValue,
            ["band_input"] = bandInput
        };

        return _evaluator.Evaluate(
            LoadDemoPack(),
            facts,
            new DateOnly(2026, 10, 2));
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

    private KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(FixturePath());

    private static JsonNode DemoD()
        => JsonNode.Parse(File.ReadAllText(FixturePath()))!;

    private static string FixturePath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-d-pack.json");

    private static void AssertCalculation(
        CalculationTrace trace,
        string id,
        string kind,
        decimal expected)
    {
        Assert.Equal(id, trace.Id);
        Assert.Equal(kind, trace.Kind);
        Assert.Equal(CaseValueKind.Number, trace.Result.Kind);
        Assert.Equal(expected, trace.Result.Number);
    }
}
