using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class NumericExpressionSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Derived_score_uses_range_lookup_max_and_sum()
    {
        var result = Evaluate(baseInput: 5m, optionA: 3m, optionB: 6m);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        var condition = Assert.IsType<RuleTrace>(result.RuleTrace).Condition;
        Assert.Equal(ConditionResult.Matched, condition.Result);
        Assert.Equal(12.5m, condition.Actual!.Value.Number);

        var expression = Assert.IsType<NumericExpressionTrace>(condition.NumericExpression);
        Assert.Equal("sum", expression.Kind);
        Assert.Equal(12.5m, expression.Value.Number);
        Assert.Equal(2, expression.Children.Count);

        var baseLookup = expression.Children[0];
        Assert.Equal("range_lookup", baseLookup.Kind);
        Assert.Equal(5m, baseLookup.Value.Number);
        Assert.Equal(5m, baseLookup.SelectedMinimum);
        Assert.Equal(9m, baseLookup.SelectedMaximum);
        Assert.Equal(5m, baseLookup.SelectedValue);

        var alternatives = expression.Children[1];
        Assert.Equal("max", alternatives.Kind);
        Assert.Equal(7.5m, alternatives.Value.Number);
        Assert.Equal(2, alternatives.Children.Count);
    }

    [Fact]
    public void Range_lookup_is_inclusive_at_band_boundaries()
    {
        var result = Evaluate(baseInput: 9m, optionA: 6m, optionB: 6m);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        var lookup = result.RuleTrace!.Condition.NumericExpression!.Children[0];
        Assert.Equal(5m, lookup.Value.Number);
        Assert.Equal(5m, lookup.SelectedMinimum);
        Assert.Equal(9m, lookup.SelectedMaximum);
    }

    [Fact]
    public void Missing_expression_input_propagates_unknown_instead_of_zero()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_input"] = 5m,
            ["option_a"] = 6m
        };

        var result = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);
        var expression = Assert.IsType<NumericExpressionTrace>(result.RuleTrace.Condition.NumericExpression);
        Assert.True(expression.Value.IsUnknown);
        Assert.True(expression.Children[1].Value.IsUnknown);
    }

    [Fact]
    public void Input_outside_lookup_bands_fails_closed_as_unknown()
    {
        var result = Evaluate(baseInput: 5m, optionA: 6m, optionB: 11m);

        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.ConditionResult);

        var max = result.RuleTrace.Condition.NumericExpression!.Children[1];
        Assert.True(max.Value.IsUnknown);
        Assert.True(max.Children[1].Value.IsUnknown);
        Assert.Null(max.Children[1].SelectedMinimum);
        Assert.Null(max.Children[1].SelectedMaximum);
        Assert.Null(max.Children[1].SelectedValue);
    }

    [Fact]
    public void Overlapping_lookup_bands_are_rejected()
    {
        var node = DemoD();
        var bands = node["rules"]![0]!["condition"]!["numericExpression"]!["operands"]![0]!["bands"]!.AsArray();
        bands[1]!["minimum"] = 4m;

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "invalid_numeric_band_order");
    }

    [Fact]
    public void Lookup_band_requires_explicit_bounds_and_value()
    {
        var node = DemoD();
        var band = node["rules"]![0]!["condition"]!["numericExpression"]!["operands"]![0]!["bands"]![0]!.AsObject();
        band.Remove("minimum");

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "incomplete_numeric_band");
    }

    [Fact]
    public void Empty_max_expression_is_rejected()
    {
        var node = DemoD();
        node["rules"]![0]!["condition"]!["numericExpression"]!["operands"]![1]!["operands"] = new JsonArray();

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "empty_numeric_operands");
    }

    [Fact]
    public void Numeric_expression_cannot_reference_a_truth_field()
    {
        var node = DemoD();
        node["fields"]![0]!["type"] = "truth";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "numeric_expression_field_type_mismatch");
    }

    [Fact]
    public void Numeric_condition_cannot_mix_raw_field_and_expression_inputs()
    {
        var node = DemoD();
        node["rules"]![0]!["condition"]!["field"] = "base_input";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "ambiguous_numeric_input");
    }

    [Fact]
    public void Repeated_evaluation_produces_identical_trace()
    {
        var pack = LoadDemoPack();
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_input"] = 10m,
            ["option_a"] = 5m,
            ["option_b"] = 6m
        };

        var first = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));
        var second = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    private AssessmentResult Evaluate(decimal baseInput, decimal optionA, decimal optionB)
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["base_input"] = baseInput,
            ["option_a"] = optionA,
            ["option_b"] = optionB
        };

        return _evaluator.Evaluate(
            LoadDemoPack(),
            facts,
            new DateOnly(2026, 10, 2));
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

    private Knowledge.Model.KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static JsonNode DemoD()
        => JsonNode.Parse(File.ReadAllText(DemoPackPath()))!;

    private static string DemoPackPath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "demo-d-pack.json");
}
