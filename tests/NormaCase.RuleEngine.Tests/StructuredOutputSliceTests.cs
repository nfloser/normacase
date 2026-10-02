using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class StructuredOutputSliceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly RuleEvaluator _evaluator = new();

    [Fact]
    public void One_evaluation_emits_independent_typed_outputs_with_source_trace()
    {
        var result = _evaluator.Evaluate(
            LoadDemoPack(),
            new Dictionary<string, CaseValue>
            {
                ["overall_ready"] = TruthValue.Yes,
                ["segment_a_ready"] = TruthValue.Yes,
                ["segment_b_ready"] = CaseValue.Unknown,
                ["external_clearance"] = CaseValue.Unknown
            },
            new DateOnly(2026, 10, 2));

        Assert.Equal(AssessmentOutcome.Supported, result.Outcome);
        Assert.Equal(4, result.Outputs.Count);

        var decision = Assert.Single(result.Outputs, item => item.Id == "primary_result");
        Assert.Equal(StructuredOutputRole.Decision, decision.Role);
        Assert.Equal(StructuredOutputValueKind.Code, decision.Value.Kind);
        Assert.Equal("READY", decision.Value.Code);

        var workflow = Assert.Single(result.Outputs, item => item.Id == "next_step");
        Assert.Equal(StructuredOutputRole.Workflow, workflow.Role);
        Assert.Equal("EXTERNAL_REVIEW", workflow.Value.Code);

        var segmentA = Assert.Single(result.Outputs, item => item.Id == "segment_state" && item.Scope == "segment_a");
        Assert.Equal(TruthValue.Yes, segmentA.Value.Truth);

        var segmentB = Assert.Single(result.Outputs, item => item.Id == "segment_state" && item.Scope == "segment_b");
        Assert.Equal(StructuredOutputValueKind.Unknown, segmentB.Value.Kind);

        Assert.All(result.Outputs, item =>
        {
            Assert.Equal("DEMO-E-MULTI-OUTPUT", item.RuleId);
            Assert.Equal(1, item.RuleVersion);
            Assert.Equal("SYNTH-DEMO-E-001", item.Source.Id);
            Assert.Equal("1", item.Source.Version);
            Assert.Equal("repository:knowledge/demo-e/pack.json", item.Source.SourceLocation);
        });
    }

    [Fact]
    public void Duplicate_output_identity_is_rejected_but_same_id_with_different_scope_is_allowed()
    {
        var node = DemoE();
        var outputs = node["rules"]![0]!["outputs"]!.AsArray();
        outputs.Add(outputs[2]!.DeepClone());

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "duplicate_output_identity");
    }

    [Fact]
    public void Code_output_must_bound_every_emitted_code()
    {
        var node = DemoE();
        node["rules"]![0]!["outputs"]![0]!["onMatch"]!["code"] = "UNDECLARED";

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "output_code_not_allowed");
    }

    [Fact]
    public void Output_value_kind_must_match_declared_type()
    {
        var node = DemoE();
        var onMatch = node["rules"]![0]!["outputs"]![2]!["onMatch"]!.AsObject();
        onMatch.Clear();
        onMatch["kind"] = "NUMBER";
        onMatch["number"] = 1m;

        var error = LoadFailure(node);

        Assert.Contains(error.Errors, item => item.Code == "output_value_type_mismatch");
    }

    [Fact]
    public void Output_unknown_is_preserved_instead_of_becoming_negative()
    {
        var result = _evaluator.Evaluate(
            LoadDemoPack(),
            new Dictionary<string, CaseValue>
            {
                ["overall_ready"] = TruthValue.Yes
            },
            new DateOnly(2026, 10, 2));

        var segmentA = Assert.Single(result.Outputs, item => item.Id == "segment_state" && item.Scope == "segment_a");
        var segmentB = Assert.Single(result.Outputs, item => item.Id == "segment_state" && item.Scope == "segment_b");

        Assert.Equal(StructuredOutputValueKind.Unknown, segmentA.Value.Kind);
        Assert.Equal(StructuredOutputValueKind.Unknown, segmentB.Value.Kind);
    }

    private KnowledgeValidationException LoadFailure(JsonNode node)
        => Assert.Throws<KnowledgeValidationException>(() => _loader.LoadFromJson(node.ToJsonString()));

    private NormaCase.Knowledge.Model.KnowledgePack LoadDemoPack()
        => _loader.LoadFromFile(DemoPackPath());

    private static JsonNode DemoE()
        => JsonNode.Parse(File.ReadAllText(DemoPackPath()))!;

    private static string DemoPackPath()
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-e-pack.json");
}
