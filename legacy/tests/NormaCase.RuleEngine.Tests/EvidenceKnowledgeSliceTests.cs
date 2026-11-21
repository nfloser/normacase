using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class EvidenceKnowledgeSliceTests
{
    private static readonly DateOnly Date = new(2026, 10, 2);
    private static string Json => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-c-pack.json"));
    private static Dictionary<string, CaseValue> Facts => new()
    {
        ["request_confirmed"] = TruthValue.Yes,
        ["measurement"] = 15m,
        ["alternative_confirmed"] = TruthValue.No
    };

    [Fact]
    public void Complete_evidence_produces_source_backed_result()
    {
        var result = Evaluate(new() { ["verification"] = EvidenceStatus.Present });
        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal("demo-c-2026.1", result.KnowledgeRelease);
        Assert.Equal("SYNTH-DEMO-C-001", result.RuleTrace!.SourceId);
        var gate = result.RuleTrace.Condition.Children[1];
        Assert.Equal("verification", gate.EvidenceRequirementId);
        Assert.Equal(EvidenceStatus.Present, gate.EvidenceStatus);
        Assert.Equal(ConditionResult.Matched, gate.Result);
    }

    [Theory]
    [InlineData(EvidenceStatus.Missing)]
    [InlineData(EvidenceStatus.Conflicting)]
    public void Unavailable_evidence_never_becomes_a_positive_or_negative_decision(EvidenceStatus status)
    {
        var result = Evaluate(new() { ["verification"] = status });
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        var gate = result.RuleTrace!.Condition.Children[1];
        Assert.Equal(ConditionResult.Unknown, gate.Result);
        Assert.Equal(status, gate.EvidenceStatus);
    }

    [Fact]
    public void Omitted_evidence_is_explicitly_missing_in_trace()
    {
        var result = Evaluate(new());
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Equal(EvidenceStatus.Missing, result.RuleTrace!.Condition.Children[1].EvidenceStatus);
    }

    [Fact]
    public void Missing_evidence_defaults_to_incomplete_when_no_escalation_is_configured()
    {
        var json = Json.Replace(",\n      \"onUnknown\": \"HUMAN_REVIEW\"", "", StringComparison.Ordinal);
        Assert.Equal(AssessmentOutcome.Incomplete, Evaluate(new(), json).Outcome);
    }

    [Fact]
    public void Complete_evidence_preserves_no_match_and_nested_alternative()
    {
        var facts = Facts;
        facts["measurement"] = 5m;
        var pack = new KnowledgePackLoader().LoadFromJson(Json);
        var evidence = new Dictionary<string, EvidenceStatus> { ["verification"] = EvidenceStatus.Present };
        var evaluator = new RuleEvaluator();
        Assert.Equal(AssessmentOutcome.NotSupported, evaluator.Evaluate(pack, facts, Date, evidence).Outcome);
        facts["alternative_confirmed"] = TruthValue.Yes;
        Assert.Equal(AssessmentOutcome.Supported, evaluator.Evaluate(pack, facts, Date, evidence).Outcome);
    }

    [Fact]
    public void Missing_required_fact_still_forces_incomplete()
    {
        var facts = Facts;
        facts.Remove("request_confirmed");
        var pack = new KnowledgePackLoader().LoadFromJson(Json);
        Assert.Equal(AssessmentOutcome.Incomplete,
            new RuleEvaluator().Evaluate(pack, facts, Date, new Dictionary<string, EvidenceStatus>()).Outcome);
    }

    [Theory]
    [InlineData("\"evidenceRequirementId\": \"verification\"", "\"evidenceRequirementId\": \"undeclared\"", "missing_evidence_requirement")]
    [InlineData("\"onUnknown\": \"HUMAN_REVIEW\"", "\"onUnknown\": \"SUPPORTED\"", "unsafe_on_unknown")]
    public void Invalid_knowledge_fails_closed(string before, string after, string code)
    {
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(Json.Replace(before, after, StringComparison.Ordinal)));
        Assert.Contains(error.Errors, item => item.Code == code);
    }

    [Fact]
    public void Undeclared_runtime_evidence_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(new() { ["undeclared"] = EvidenceStatus.Present }));
    }

    [Fact]
    public void Invalid_runtime_evidence_status_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(new() { ["verification"] = (EvidenceStatus)99 }));
    }


    [Theory]
    [InlineData("[]", "invalid_evidence_dependency")]
    [InlineData("[{\"kind\":\"number_gte\",\"field\":\"measurement\",\"threshold\":10},{\"kind\":\"number_gte\",\"field\":\"measurement\",\"threshold\":10}]", "invalid_evidence_dependency")]
    public void Evidence_gate_requires_exactly_one_dependency(string children, string code)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Json)!;
        node["rules"]![0]!["condition"]!["conditions"]![1]!["conditions"] =
            System.Text.Json.Nodes.JsonNode.Parse(children);
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
        Assert.Contains(error.Errors, item => item.Code == code);
    }

    [Fact]
    public void Duplicate_evidence_requirement_ids_are_rejected()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Json)!;
        node["evidenceRequirements"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("{\"id\":\"verification\"}"));
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
        Assert.Contains(error.Errors, item => item.Code == "duplicate_evidence_requirement_id");
    }

    [Fact]
    public void Conflicting_evidence_requires_review_even_when_another_child_is_false()
    {
        var facts = Facts;
        facts["request_confirmed"] = TruthValue.No;
        var pack = new KnowledgePackLoader().LoadFromJson(Json);
        var evidence = new Dictionary<string, EvidenceStatus> { ["verification"] = EvidenceStatus.Conflicting };
        var result = new RuleEvaluator().Evaluate(pack, facts, Date, evidence);
        Assert.Equal(ConditionResult.NotMatched, result.RuleTrace!.ConditionResult);
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
    }

    [Fact]
    public void Present_evidence_does_not_fill_missing_child_facts()
    {
        var facts = Facts;
        facts.Remove("measurement");
        var pack = new KnowledgePackLoader().LoadFromJson(Json);
        var evidence = new Dictionary<string, EvidenceStatus> { ["verification"] = EvidenceStatus.Present };
        var result = new RuleEvaluator().Evaluate(pack, facts, Date, evidence);
        Assert.Equal(AssessmentOutcome.HumanReview, result.Outcome);
        Assert.Equal(ConditionResult.Unknown, result.RuleTrace!.Condition.Children[1].Result);
    }

    [Fact]
    public void Evidence_gate_cannot_silently_ignore_a_comparison()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(Json)!;
        node["rules"]![0]!["condition"]!["conditions"]![1]!["field"] = "measurement";
        var error = Assert.Throws<KnowledgeValidationException>(() =>
            new KnowledgePackLoader().LoadFromJson(node.ToJsonString()));
        Assert.Contains(error.Errors, item => item.Code == "ambiguous_evidence_dependency");
    }

    [Fact]
    public void Repeated_evaluation_preserves_the_entire_serialized_trace()
    {
        var evidence = new Dictionary<string, EvidenceStatus> { ["verification"] = EvidenceStatus.Present };
        var first = Evaluate(evidence);
        var second = Evaluate(evidence);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first), System.Text.Json.JsonSerializer.Serialize(second));
    }

    private static AssessmentResult Evaluate(Dictionary<string, EvidenceStatus> evidence, string? json = null)
        => new RuleEvaluator().Evaluate(new KnowledgePackLoader().LoadFromJson(json ?? Json), Facts, Date, evidence);
}
