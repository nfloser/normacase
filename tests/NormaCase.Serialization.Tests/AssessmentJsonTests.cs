using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class AssessmentJsonTests
{
    [Theory]
    [InlineData("demo-a")]
    [InlineData("demo-b")]
    [InlineData("demo-c")]
    [InlineData("demo-e")]
    public void Entire_evaluated_assessment_roundtrips_without_changing_values(string demo)
    {
        var result = Evaluate(demo);
        var json = AssessmentJson.Serialize(result, "test-platform-1");
        var restored = AssessmentJson.Deserialize(json);
        Assert.Equal(1, restored.FormatVersion);
        Assert.Equal("test-platform-1", restored.PlatformVersion);
        Assert.Equal(json, AssessmentJson.Serialize(restored.Assessment, restored.PlatformVersion));
        Assert.Equal(result.RuleTrace!.Source, restored.Assessment.RuleTrace!.Source);
        Assert.Equal(result.RuleTrace.Condition.Children[0].Actual, restored.Assessment.RuleTrace.Condition.Children[0].Actual);
        Assert.Equal(result.Outputs, restored.Assessment.Outputs);
    }

    [Fact]
    public void Structured_output_value_shapes_are_strictly_validated()
    {
        var result = Evaluate("demo-e");
        var node = JsonNode.Parse(AssessmentJson.Serialize(result, "1"))!;
        node["assessment"]!["outputs"]![0]!["value"]!["unexpected"] = true;

        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Theory]
    [InlineData(TruthValue.Yes)]
    [InlineData(TruthValue.No)]
    [InlineData(TruthValue.NotApplicable)]
    [InlineData(TruthValue.Unknown)]
    public void All_truth_states_roundtrip(TruthValue truth)
    {
        var value = CaseValue.FromTruth(truth);
        var result = WithValue(value);
        var restored = AssessmentJson.Deserialize(AssessmentJson.Serialize(result, "1"));
        Assert.Equal(value, restored.Assessment.RuleTrace!.Condition.Actual);
    }

    [Fact]
    public void Decimal_precision_and_zero_are_preserved()
    {
        foreach (var number in new[] { 0m, -1m, 123456789.1234567890123456789m, decimal.MaxValue, decimal.MinValue })
        {
            var value = CaseValue.FromNumber(number);
            var restored = AssessmentJson.Deserialize(AssessmentJson.Serialize(WithValue(value), "1"));
            Assert.Equal(value, restored.Assessment.RuleTrace!.Condition.Actual);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"kind\":\"UNKNOWN\",\"number\":0}")]
    [InlineData("{\"kind\":\"NUMBER\",\"number\":\"10\"}")]
    [InlineData("{\"kind\":\"NUMBER\",\"number\":1e100}")]
    [InlineData("{\"kind\":\"NUMBER\",\"number\":10,\"truth\":\"YES\"}")]
    [InlineData("{\"kind\":\"TRUTH\",\"truth\":\"UNKNOWN\"}")]
    [InlineData("{\"kind\":\"TRUTH\",\"truth\":1}")]
    [InlineData("{\"kind\":\"TRUTH\",\"truth\":\"MAGIC\"}")]
    [InlineData("{\"kind\":\"TRUTH\",\"truth\":\"1\"}")]
    [InlineData("{\"kind\":\"MAGIC\"}")]
    [InlineData("{\"kind\":\"NUMBER\",\"number\":1e-29}")]
    [InlineData("{\"kind\":\"NUMBER\",\"number\":0.12345678901234567890123456789}")]
    public void Malformed_values_are_rejected(string value)
    {
        var node = JsonNode.Parse(AssessmentJson.Serialize(WithValue(CaseValue.Unknown), "1"))!;
        node["assessment"]!["ruleTrace"]!["condition"]!["actual"] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Theory]
    [InlineData("formatVersion", "2")]
    [InlineData("platformVersion", "\"\"")]
    [InlineData("assessment", "null")]
    [InlineData("unexpected", "true")]
    public void Invalid_envelopes_are_rejected(string property, string jsonValue)
    {
        var node = JsonNode.Parse(AssessmentJson.Serialize(Evaluate("demo-a"), "1"))!;
        node[property] = JsonNode.Parse(jsonValue);
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Missing_required_constructor_values_are_rejected()
    {
        var node = JsonNode.Parse(AssessmentJson.Serialize(Evaluate("demo-a"), "1"))!;
        node["assessment"]!.AsObject().Remove("outcome");
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Numeric_enum_values_are_rejected()
    {
        var node = JsonNode.Parse(AssessmentJson.Serialize(Evaluate("demo-a"), "1"))!;
        node["assessment"]!["outcome"] = 0;
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Numeric_string_enum_values_are_rejected()
    {
        var node = JsonNode.Parse(AssessmentJson.Serialize(Evaluate("demo-a"), "1"))!;
        node["assessment"]!["outcome"] = "0";
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Duplicate_properties_are_rejected_at_every_level()
    {
        var json = AssessmentJson.Serialize(WithValue(CaseValue.Unknown), "1");
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(json.Replace("\"kind\":\"UNKNOWN\"", "\"kind\":\"NUMBER\",\"kind\":\"UNKNOWN\"")));
        Assert.Throws<JsonException>(() => AssessmentJson.Deserialize(json.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")));
    }

    [Fact]
    public void No_active_rule_result_roundtrips()
    {
        var pack = Load("demo-a");
        var result = new RuleEvaluator().Evaluate(pack, new Dictionary<string, CaseValue>(), new DateOnly(2025, 1, 1));
        var restored = AssessmentJson.Deserialize(AssessmentJson.Serialize(result, "1"));
        Assert.Equal(AssessmentOutcome.HumanReview, restored.Assessment.Outcome);
        Assert.Null(restored.Assessment.RuleTrace);
    }

    private static AssessmentResult WithValue(CaseValue value)
    {
        var result = Evaluate("demo-a");
        var condition = result.RuleTrace!.Condition with { Actual = value };
        return result with { RuleTrace = result.RuleTrace with { Condition = condition } };
    }

    private static AssessmentResult Evaluate(string demo)
    {
        var facts = demo switch
        {
            "demo-a" => new Dictionary<string, CaseValue> { ["criterion_a"] = TruthValue.Yes, ["criterion_b"] = TruthValue.No },
            "demo-b" => new Dictionary<string, CaseValue> { ["score"] = 12.123456789m, ["band_value"] = 30m },
            "demo-c" => new Dictionary<string, CaseValue> { ["request_confirmed"] = TruthValue.Yes, ["measurement"] = 15m },
            "demo-e" => new Dictionary<string, CaseValue>
            {
                ["overall_ready"] = TruthValue.Yes,
                ["segment_a_ready"] = TruthValue.Yes,
                ["segment_b_ready"] = CaseValue.Unknown,
                ["external_clearance"] = CaseValue.Unknown
            },
            _ => throw new ArgumentOutOfRangeException(nameof(demo))
        };
        var evidence = demo == "demo-c"
            ? new Dictionary<string, EvidenceStatus> { ["verification"] = EvidenceStatus.Present }
            : null;
        return new RuleEvaluator().Evaluate(Load(demo), facts, new DateOnly(2026, 10, 2), evidence);
    }

    private static NormaCase.Knowledge.Model.KnowledgePack Load(string demo)
        => new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
}
