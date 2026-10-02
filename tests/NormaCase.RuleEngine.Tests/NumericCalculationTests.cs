using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class NumericCalculationTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Nested_range_max_and_sum_produce_a_traceable_score()
    {
        var result = Evaluate(5m, 5m, 3m);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);

        var condition = result.RuleTrace!.Condition;
        Assert.Equal(ConditionResult.Matched, condition.Result);
        Assert.Null(condition.Field);
        Assert.Equal("composite_score", condition.CalculationId);
        Assert.Equal(14m, condition.Actual!.Value.Number);

        var calculation = Assert.IsType<NumericCalculationTrace>(condition.Calculation);
        Assert.Equal("sum", calculation.Kind);
        Assert.Equal(14m, calculation.Result.Number);
        Assert.Equal(2, calculation.Children.Count);

        var firstLookup = calculation.Children[0];
        Assert.Equal("range_lookup", firstLookup.Kind);
        Assert.Equal(8m, firstLookup.Result.Number);
        Assert.Equal(4m, firstLookup.SelectedMinimum);
        Assert.Equal(5m, firstLookup.SelectedMaximum);
        Assert.Equal(8m, firstLookup.SelectedValue);
        Assert.Equal("channel_a", firstLookup.Children[0].Field);

        var max = calculation.Children[1];
        Assert.Equal("max", max.Kind);
        Assert.Equal(6m, max.Result.Number);
    }

    [Fact]
    public void Max_uses_only_the_largest_operand()
    {
        var result = Evaluate(5m, 8m, 8m);

        var calculation = Assert.IsType<NumericCalculationTrace>(
            result.RuleTrace!.Condition.Calculation);

        Assert.Equal(20m, calculation.Result.Number);
        Assert.Equal(12m, calculation.Children[1].Result.Number);
    }

    [Theory]
    [InlineData("channel_a", 2, 4)]
    [InlineData("channel_a", 3, 4)]
    [InlineData("channel_b", 6, 10)]
    [InlineData("channel_c", 5, 6)]
    public void Inclusive_band_boundaries_select_the_declared_value(
        string field,
        decimal input,
        decimal expected)
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["channel_a"] = field == "channel_a" ? input : 0m,
            ["channel_b"] = field == "channel_b" ? input : 0m,
            ["channel_c"] = field == "channel_c" ? input : 0m
        };

        var result = _evaluator.Evaluate(
            LoadDemoPack(),
            facts,
            new DateOnly(2026, 10, 2));

        var calculation = Assert.IsType<NumericCalculationTrace>(
            result.RuleTrace!.Condition.Calculation);
        var lookups = Descendants(calculation)
            .Where(item => item.Kind == "range_lookup")
            .ToArray();

        Assert.Contains(lookups, item => item.Result.Number == expected);
    }

    [Fact]
    public void Missing_required_input_propagates_unknown_and_incomplete()
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["channel_a"] = 5m,
            ["channel_b"] = 5m
        };

        var result = _evaluator.Evaluate(
            LoadDemoPack(),
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("channel_c", result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
        Assert.True(result.RuleTrace.Condition.Calculation!.Result.IsUnknown);
    }

    [Fact]
    public void Known_value_outside_declared_bands_fails_closed()
    {
        var result = Evaluate(1.5m, 5m, 3m);

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);

        var calculation = Assert.IsType<NumericCalculationTrace>(
            result.RuleTrace.Condition.Calculation);
        var lookup = calculation.Children[0];

        Assert.True(lookup.Result.IsUnknown);
        Assert.Null(lookup.SelectedValue);
    }

    [Fact]
    public void Overlapping_inclusive_bands_are_rejected()
    {
        var node = DemoNode();
        var bands = node["calculations"]![0]!["expression"]!["operands"]![0]!["bands"]!.AsArray();
        bands.Add(new JsonObject
        {
            ["minimum"] = 1,
            ["maximum"] = 2,
            ["value"] = 99
        });

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "overlapping_numeric_bands");
    }

    [Fact]
    public void Empty_sum_is_rejected()
    {
        var node = DemoNode();
        node["calculations"]![0]!["expression"]!["operands"] = new JsonArray();

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "empty_numeric_operands");
    }

    [Fact]
    public void Numeric_expression_cannot_reference_truth_field()
    {
        var node = DemoNode();
        node["fields"]![0]!["type"] = "truth";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "numeric_expression_field_type_mismatch");
    }

    [Fact]
    public void Condition_cannot_reference_unknown_calculation()
    {
        var node = DemoNode();
        node["rules"]![0]!["condition"]!["calculationId"] = "missing";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "missing_calculation");
    }

    [Fact]
    public void Numeric_condition_cannot_mix_field_and_calculation_targets()
    {
        var node = DemoNode();
        node["rules"]![0]!["condition"]!["field"] = "channel_a";

        var exception = LoadFailure(node);

        Assert.Contains(
            exception.Errors,
            error => error.Code == "ambiguous_numeric_target");
    }

    private AssessmentResult Evaluate(decimal channelA, decimal channelB, decimal channelC)
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["channel_a"] = channelA,
            ["channel_b"] = channelB,
            ["channel_c"] = channelC
        };

        return _evaluator.Evaluate(
            LoadDemoPack(),
            facts,
            new DateOnly(2026, 10, 2));
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

    private static IEnumerable<NumericCalculationTrace> Descendants(
        NumericCalculationTrace trace)
    {
        yield return trace;

        foreach (var child in trace.Children)
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private JsonNode DemoNode()
        => JsonNode.Parse(File.ReadAllText(DemoPackPath()))!;

    private NormaCase.Knowledge.Model.KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static string DemoPackPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-d-pack.json");
}
