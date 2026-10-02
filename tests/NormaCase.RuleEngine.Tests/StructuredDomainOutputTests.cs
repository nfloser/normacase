using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class StructuredDomainOutputTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void Independent_outputs_keep_known_siblings_when_one_is_unknown()
    {
        var result = EvaluateDemoE(includeBeta: false);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(4, result.DomainOutputs.Count);

        AssertChoice(result, "decision_state", "ELIGIBLE");
        AssertChoice(result, "selection_state", "MODE_RED");
        AssertChoice(result, "segment_alpha", "OPEN");

        var beta = Assert.Single(result.DomainOutputs, item => item.OutputId == "segment_beta");
        Assert.Equal(DomainOutputValueKind.Unknown, beta.Value.Kind);
        Assert.Null(beta.Value.Choice);
        Assert.Equal(ConditionResult.Unknown, beta.ConditionResult);
        Assert.Equal(ConditionResult.Unknown, beta.Condition.Result);
    }

    [Fact]
    public void Output_trace_captures_version_condition_and_detached_source_revision()
    {
        var result = EvaluateDemoE(includeBeta: true);
        var output = Assert.Single(result.DomainOutputs, item => item.OutputId == "decision_state");

        Assert.Equal(1, output.OutputVersion);
        Assert.Equal(DomainOutputValueKind.Choice, output.Value.Kind);
        Assert.Equal("ELIGIBLE", output.Value.Choice);
        Assert.Equal(ConditionResult.Matched, output.ConditionResult);
        Assert.Equal("all", output.Condition.Kind);
        Assert.Equal("SYNTH-DEMO-E-001", output.Source.Id);
        Assert.Equal("1", output.Source.Version);
        Assert.Equal("repository:knowledge/demo-e/pack.json", output.Source.SourceLocation);
    }

    [Fact]
    public void Outputs_are_evaluated_from_their_own_conditions()
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["gate_primary"] = TruthValue.Yes,
            ["metric"] = 14m,
            ["segment_alpha_ready"] = TruthValue.No,
            ["segment_beta_ready"] = TruthValue.Yes
        };

        var result = _evaluator.Evaluate(
            LoadDemoE(),
            facts,
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        AssertChoice(result, "decision_state", "ELIGIBLE");
        AssertChoice(result, "selection_state", "MODE_BLUE");
        AssertChoice(result, "segment_alpha", "CLOSED");
        AssertChoice(result, "segment_beta", "OPEN");
    }

    [Fact]
    public void Existing_pack_without_outputs_returns_empty_output_collection()
    {
        var pack = _loader.LoadFromFile(FixturePath("demo-a"));
        var facts = new Dictionary<string, CaseValue>
        {
            ["criterion_a"] = TruthValue.Yes,
            ["criterion_b"] = TruthValue.No
        };

        var result = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));

        Assert.Empty(pack.Outputs);
        Assert.Empty(result.DomainOutputs);
    }

    [Fact]
    public void Unknown_output_does_not_change_existing_assessment_outcome()
    {
        var result = EvaluateDemoE(includeBeta: false);

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Contains(
            result.DomainOutputs,
            output => output.Value.Kind == DomainOutputValueKind.Unknown);
    }

    [Fact]
    public void Duplicate_choice_is_rejected()
    {
        var node = DemoE();
        node["outputs"]![0]!["choices"]![1] = "ELIGIBLE";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "duplicate_output_choice");
    }

    [Fact]
    public void Reserved_unknown_choice_is_rejected()
    {
        var node = DemoE();
        node["outputs"]![0]!["choices"]![1] = "UNKNOWN";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "reserved_output_choice");
    }

    [Fact]
    public void Branch_value_must_be_declared_by_the_output()
    {
        var node = DemoE();
        node["outputs"]![0]!["onMatch"] = "UNDECLARED";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "invalid_output_branch_value");
    }

    [Fact]
    public void Output_requires_a_known_source()
    {
        var node = DemoE();
        node["outputs"]![0]!["sourceId"] = "MISSING";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "missing_output_source");
    }

    [Fact]
    public void Output_versions_may_not_overlap()
    {
        var node = DemoE();
        var duplicate = node["outputs"]![0]!.DeepClone();
        duplicate["version"] = 2;
        duplicate["validFrom"] = "2026-06-01";
        node["outputs"]!.AsArray().Add(duplicate);

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "overlapping_output_validity");
    }

    [Fact]
    public void Output_condition_uses_existing_field_validation()
    {
        var node = DemoE();
        node["outputs"]![0]!["condition"]!["conditions"]![0]!["field"] = "missing_field";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "missing_field");
    }

    [Fact]
    public void Repeated_output_evaluation_is_deterministic()
    {
        var pack = LoadDemoE();
        var facts = DemoFacts(includeBeta: true);

        var first = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));
        var second = _evaluator.Evaluate(pack, facts, new DateOnly(2026, 10, 2));

        Assert.Equal(
            JsonSerializer.Serialize(first.DomainOutputs),
            JsonSerializer.Serialize(second.DomainOutputs));
    }

    private AssessmentResult EvaluateDemoE(bool includeBeta)
        => _evaluator.Evaluate(
            LoadDemoE(),
            DemoFacts(includeBeta),
            new DateOnly(2026, 10, 2));

    private static Dictionary<string, CaseValue> DemoFacts(bool includeBeta)
    {
        var facts = new Dictionary<string, CaseValue>
        {
            ["gate_primary"] = TruthValue.Yes,
            ["metric"] = 7m,
            ["segment_alpha_ready"] = TruthValue.Yes
        };

        if (includeBeta)
        {
            facts["segment_beta_ready"] = TruthValue.No;
        }

        return facts;
    }

    private static void AssertChoice(
        AssessmentResult result,
        string outputId,
        string expected)
    {
        var output = Assert.Single(result.DomainOutputs, item => item.OutputId == outputId);
        Assert.Equal(DomainOutputValueKind.Choice, output.Value.Kind);
        Assert.Equal(expected, output.Value.Choice);
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

    private KnowledgePack LoadDemoE()
        => _loader.LoadFromFile(FixturePath("demo-e"));

    private static JsonNode DemoE()
        => JsonNode.Parse(File.ReadAllText(FixturePath("demo-e")))!;

    private static string FixturePath(string demo)
        => Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            demo + "-pack.json");
}
