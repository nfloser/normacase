using System.Text.Json.Nodes;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using Xunit;

namespace NormaCase.RuleEngine.Tests;

public sealed class WorkflowKnowledgeTests
{
    private readonly KnowledgePackLoader _loader = new();

    [Fact]
    public void Valid_workflow_materializes_into_the_generic_domain_contract()
    {
        var node = DemoA();
        node["workflows"] = JsonNode.Parse("""
            [
              {
                "id": "synthetic.review-flow",
                "version": 2,
                "sourceId": "SYNTH-DEMO-A-001",
                "initialStateId": "submitted",
                "states": [
                  { "id": "submitted", "terminal": false },
                  { "id": "review", "terminal": false },
                  { "id": "complete", "terminal": true }
                ],
                "transitions": [
                  {
                    "id": "request_review",
                    "fromStateId": "submitted",
                    "toStateId": "review"
                  },
                  {
                    "id": "complete_review",
                    "fromStateId": "review",
                    "toStateId": "complete"
                  }
                ]
              }
            ]
            """);

        var pack = _loader.LoadFromJson(node.ToJsonString());
        var knowledge = Assert.Single(pack.Workflows);
        var workflow = KnowledgeWorkflowMaterializer.Materialize(knowledge);

        Assert.Equal("synthetic.review-flow", workflow.Id);
        Assert.Equal(2, workflow.Version);
        Assert.Equal("submitted", workflow.InitialStateId);
        Assert.Equal(3, workflow.States.Count);
        Assert.Equal(2, workflow.Transitions.Count);

        var instance = NormaCase.Domain.Workflow.WorkflowInstance.Start(workflow)
            .Apply(workflow, "request_review")
            .Apply(workflow, "complete_review");

        Assert.Equal("complete", instance.StateId);
        Assert.Equal(2, instance.Revision);
        Assert.True(instance.IsTerminal(workflow));
    }

    [Fact]
    public void Unknown_workflow_source_rejects_the_entire_pack()
    {
        var node = DemoA();
        node["workflows"] = WorkflowJson(sourceId: "missing-source");

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "missing_workflow_source"
                && error.Path == "workflows.synthetic.review-flow.sourceId");
    }

    [Fact]
    public void Invalid_workflow_graph_rejects_the_entire_pack()
    {
        var node = DemoA();
        var workflows = WorkflowJson();
        workflows[0]!["transitions"] = JsonNode.Parse("""
            [
              {
                "id": "restart",
                "fromStateId": "complete",
                "toStateId": "submitted"
              }
            ]
            """);
        node["workflows"] = workflows;

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "invalid_workflow_definition"
                && error.Path == "workflows.synthetic.review-flow");
    }

    [Fact]
    public void Duplicate_workflow_ids_are_rejected()
    {
        var node = DemoA();
        var workflows = WorkflowJson();
        workflows.Add(workflows[0]!.DeepClone());
        node["workflows"] = workflows;

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _loader.LoadFromJson(node.ToJsonString()));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "duplicate_workflow_id");
    }

    [Theory]
    [InlineData("demo-a")]
    [InlineData("demo-b")]
    [InlineData("demo-c")]
    [InlineData("demo-d")]
    [InlineData("demo-e")]
    public void Existing_packs_remain_valid_without_workflows(string demo)
    {
        var pack = _loader.LoadFromJson(Read(demo));

        Assert.Empty(pack.Workflows);
    }

    private static JsonObject DemoA()
        => (JsonNode.Parse(Read("demo-a")) as JsonObject)!;

    private static JsonArray WorkflowJson(
        string sourceId = "SYNTH-DEMO-A-001")
        => (JsonNode.Parse($$"""
            [
              {
                "id": "synthetic.review-flow",
                "version": 1,
                "sourceId": "{{sourceId}}",
                "initialStateId": "submitted",
                "states": [
                  { "id": "submitted", "terminal": false },
                  { "id": "complete", "terminal": true }
                ],
                "transitions": [
                  {
                    "id": "complete",
                    "fromStateId": "submitted",
                    "toStateId": "complete"
                  }
                ]
              }
            ]
            """) as JsonArray)!;

    private static string Read(string demo)
        => File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                demo + "-pack.json"));
}
