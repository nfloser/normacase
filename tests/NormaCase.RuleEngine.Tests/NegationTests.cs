using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class NegationTests
{
    private const string Leaf = """{"kind":"field_equals","field":"criterion_a","expected":"YES"}""";

    [Theory]
    [InlineData(TruthValue.Yes, ConditionResult.NotMatched, AssessmentOutcome.NotSupported)]
    [InlineData(TruthValue.No, ConditionResult.Matched, AssessmentOutcome.Supported)]
    [InlineData(TruthValue.Unknown, ConditionResult.Unknown, AssessmentOutcome.Incomplete)]
    [InlineData(TruthValue.NotApplicable, ConditionResult.Matched, AssessmentOutcome.Supported)]
    public void Negation_inverts_only_known_matches_and_preserves_the_child(
        TruthValue value, ConditionResult conditionResult, AssessmentOutcome outcome)
    {
        var result = Evaluate(Not(Leaf), new() { ["criterion_a"] = value });
        Assert.Equal(outcome, result.Outcome);
        var trace = result.RuleTrace!.Condition;
        Assert.Equal("not", trace.Kind);
        Assert.Equal(conditionResult, trace.Result);
        var child = Assert.Single(trace.Children);
        Assert.Equal("field_equals", child.Kind);
        Assert.Equal(CaseValue.FromTruth(value), child.Actual);
        Assert.Equal("SYNTH-DEMO-A-001", result.RuleTrace.Source.Id);
    }

    [Theory]
    [InlineData(TruthValue.Yes, ConditionResult.Matched)]
    [InlineData(TruthValue.No, ConditionResult.NotMatched)]
    [InlineData(TruthValue.Unknown, ConditionResult.Unknown)]
    [InlineData(TruthValue.NotApplicable, ConditionResult.NotMatched)]
    public void Double_negation_preserves_known_and_unknown_results(TruthValue value, ConditionResult expected)
    {
        var result = Evaluate(Not(Not(Leaf)), new() { ["criterion_a"] = value });
        Assert.Equal(expected, result.RuleTrace!.Condition.Result);
        Assert.Single(Assert.Single(result.RuleTrace.Condition.Children).Children);
    }

    [Fact]
    public void Missing_optional_fact_is_not_a_positive_negated_condition()
    {
        var result = Evaluate(Not(Leaf), new());
        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.Condition.Result);
        Assert.True(Assert.Single(result.RuleTrace.Condition.Children).Actual!.Value.IsUnknown);
    }

    [Fact]
    public void Missing_required_fact_still_overrides_a_known_negated_result()
    {
        var json = JsonNode.Parse(Pack(Not("""{"kind":"field_equals","field":"criterion_b","expected":"YES"}""")))!;
        json["fields"]![0]!["required"] = true;
        var result = new RuleEvaluator().Evaluate(new KnowledgePackLoader().LoadFromJson(json.ToJsonString()),
            new Dictionary<string, CaseValue> { ["criterion_b"] = TruthValue.No }, new DateOnly(2026, 10, 2));
        Assert.Equal(ConditionResult.Matched, result.RuleTrace!.Condition.Result);
        Assert.Equal(AssessmentOutcome.Incomplete, result.Outcome);
        Assert.Contains("criterion_a", result.MissingRequiredFields);
    }

    [Theory]
    [InlineData("all", ConditionResult.Matched)]
    [InlineData("any", ConditionResult.Unknown)]
    public void Negated_groups_retain_unknown_children(string group, ConditionResult expected)
    {
        var condition = Not("{\"kind\":\"" + group + "\",\"conditions\":[" + Leaf +
            """,{"kind":"field_equals","field":"criterion_b","expected":"YES"}]}""");
        var result = Evaluate(condition, new() { ["criterion_a"] = TruthValue.No });
        Assert.Equal(expected, result.RuleTrace!.Condition.Result);
        var children = Assert.Single(result.RuleTrace.Condition.Children).Children;
        Assert.Equal(2, children.Count);
        Assert.Equal(ConditionResult.Unknown, children[1].Result);
    }

    [Theory]
    [InlineData(EvidenceStatus.Missing, ConditionResult.Unknown, AssessmentOutcome.Incomplete)]
    [InlineData(EvidenceStatus.Conflicting, ConditionResult.Unknown, AssessmentOutcome.HumanReview)]
    [InlineData(EvidenceStatus.Present, ConditionResult.Matched, AssessmentOutcome.Supported)]
    public void Negation_never_turns_unavailable_or_conflicting_evidence_into_support(
        EvidenceStatus status, ConditionResult expected, AssessmentOutcome outcome)
    {
        var gate = """{"kind":"requires_evidence","evidenceRequirementId":"synthetic-check","conditions":[""" + Leaf + "]}";
        var result = Evaluate(Not(gate), new() { ["criterion_a"] = TruthValue.No },
            new() { ["synthetic-check"] = status });
        Assert.Equal(expected, result.RuleTrace!.Condition.Result);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(status, Assert.Single(result.RuleTrace.Condition.Children).EvidenceStatus);
    }

    [Fact]
    public void A_matching_sibling_cannot_hide_conflicting_evidence_under_negation()
    {
        var gate = """{"kind":"requires_evidence","evidenceRequirementId":"synthetic-check","conditions":[""" + Leaf + "]}";
        var condition = "{\"kind\":\"any\",\"conditions\":[" + Not(gate) + "," + Leaf + "]}";
        var result = Evaluate(condition, new() { ["criterion_a"] = TruthValue.Yes },
            new() { ["synthetic-check"] = EvidenceStatus.Conflicting });
        Assert.Equal(ConditionResult.Matched, result.RuleTrace!.Condition.Result);
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
    }

    [Theory]
    [InlineData("""{"kind":"not","conditions":[]}""", "invalid_negation")]
    [InlineData("""{"kind":"not","conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"},{"kind":"field_equals","field":"criterion_b","expected":"YES"}]}""", "invalid_negation")]
    [InlineData("""{"kind":"not","field":"criterion_a","conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","expected":"NO","conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","threshold":1,"conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","minimum":1,"conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","maximum":1,"conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","numericExpression":{"kind":"field","field":"criterion_a"},"conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    [InlineData("""{"kind":"not","evidenceRequirementId":"synthetic-check","conditions":[{"kind":"field_equals","field":"criterion_a","expected":"YES"}]}""", "ambiguous_negation")]
    public void Invalid_negation_shapes_are_rejected(string condition, string code)
    {
        var exception = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(Pack(condition)));
        Assert.Contains(exception.Errors, error => error.Code == code);
    }

    [Theory]
    [InlineData("9.999999999999999999999999999", ConditionResult.Matched)]
    [InlineData("10", ConditionResult.NotMatched)]
    [InlineData("10.00000000000000000000000001", ConditionResult.NotMatched)]
    [InlineData("UNKNOWN", ConditionResult.Unknown)]
    public void Negated_numeric_threshold_preserves_exact_boundaries(string number, ConditionResult expected)
    {
        var value = number == "UNKNOWN" ? CaseValue.Unknown :
            CaseValue.FromNumber(decimal.Parse(number, System.Globalization.CultureInfo.InvariantCulture));
        var result = Evaluate(Not("""{"kind":"number_gte","field":"synthetic_number","threshold":10}"""),
            new() { ["synthetic_number"] = value });
        Assert.Equal(expected, result.RuleTrace!.Condition.Result);
        Assert.Equal(value, Assert.Single(result.RuleTrace.Condition.Children).Actual);
    }

    [Fact]
    public void An_invalid_child_is_validated_recursively()
    {
        var exception = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(Pack(Not("""{"kind":"unsupported"}"""))));
        Assert.Contains(exception.Errors, error => error.Code == "unknown_condition_kind");
    }

    private static AssessmentResult Evaluate(string condition, Dictionary<string, CaseValue> facts,
        Dictionary<string, EvidenceStatus>? evidence = null) =>
        new RuleEvaluator().Evaluate(new KnowledgePackLoader().LoadFromJson(Pack(condition)),
            facts, new DateOnly(2026, 10, 2), evidence);

    private static string Not(string child) => "{\"kind\":\"not\",\"conditions\":[" + child + "]}";

    private static string Pack(string condition)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-a-pack.json")))!;
        node["manifest"]!["releaseId"] = "synthetic-negation-tests-1";
        foreach (var field in node["fields"]!.AsArray()) field!["required"] = false;
        node["fields"]!.AsArray().Add(JsonNode.Parse("""{"id":"synthetic_number","type":"number","required":false}"""));
        node["evidenceRequirements"] = JsonNode.Parse("""[{"id":"synthetic-check"}]""");
        node["rules"]![0]!["condition"] = JsonNode.Parse(condition);
        return node.ToJsonString();
    }
}
