using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Presentation;
using NormaCase.Knowledge.Serialization;
using NormaCase.RuleEngine.Evaluation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class WorkflowPresentationTests
{
    [Theory]
    [InlineData(TruthValue.Yes, AssessmentOutcome.Supported)]
    [InlineData(TruthValue.No, AssessmentOutcome.NotSupported)]
    [InlineData(TruthValue.Unknown, AssessmentOutcome.Incomplete)]
    public void New_demo_preserves_unknown_and_does_not_infer_workflow_transitions(TruthValue value, AssessmentOutcome outcome)
    {
        var result = new RuleEvaluator().Evaluate(Pack(), new Dictionary<string, CaseValue> { ["ready"] = value }, new DateOnly(2026, 10, 3));
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal("SYNTH-DEMO-F-001", result.RuleTrace!.SourceId);
    }

    [Fact]
    public void Exact_workflow_graph_has_external_read_only_German_labels()
    {
        var presentation = new KnowledgePresentationLoader().LoadFromJson(Pack(), Json(), "de-DE");
        var workflow = Assert.Single(presentation.Workflows);
        Assert.Equal("synthetic.review", workflow.Id);
        Assert.Equal(1, workflow.Version);
        Assert.Equal("In Prüfung", workflow.States["review"]);
        Assert.Equal("Prüfung abschließen", workflow.Transitions["finish"]);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)workflow.States).Clear());
    }

    [Theory]
    [InlineData("id", "\"missing\"")]
    [InlineData("version", "2")]
    [InlineData("label", "\" \"")]
    [InlineData("states", "{}")]
    [InlineData("transitions", "{}")]
    public void Unknown_workflow_version_or_missing_labels_fail_closed(string property, string token)
    {
        var node = JsonNode.Parse(Json())!;
        node["workflows"]![0]![property] = JsonNode.Parse(token);
        Assert.Throws<InvalidOperationException>(() => new KnowledgePresentationLoader().LoadFromJson(Pack(), node.ToJsonString(), "de-DE"));
    }

    [Fact]
    public void Missing_duplicate_null_and_blank_workflow_labels_fail_closed()
    {
        var node = JsonNode.Parse(Json())!;
        node.AsObject().Remove("workflows");
        Assert.Throws<InvalidOperationException>(() => new KnowledgePresentationLoader().LoadFromJson(Pack(), node.ToJsonString(), "de-DE"));
        node = JsonNode.Parse(Json())!;
        node["workflows"]!.AsArray().Add(node["workflows"]![0]!.DeepClone());
        Assert.Throws<InvalidOperationException>(() => new KnowledgePresentationLoader().LoadFromJson(Pack(), node.ToJsonString(), "de-DE"));
        node = JsonNode.Parse(Json())!;
        node["workflows"]![0] = null;
        Assert.Throws<JsonException>(() => new KnowledgePresentationLoader().LoadFromJson(Pack(), node.ToJsonString(), "de-DE"));
        node = JsonNode.Parse(Json())!;
        node["workflows"]![0]!["states"]!["draft"] = " ";
        Assert.Throws<InvalidOperationException>(() => new KnowledgePresentationLoader().LoadFromJson(Pack(), node.ToJsonString(), "de-DE"));
    }

    private static NormaCase.Knowledge.Model.KnowledgePack Pack()
        => new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-f-pack.json"));
    private static string Json() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-f-presentation.de-DE.json"));
}
