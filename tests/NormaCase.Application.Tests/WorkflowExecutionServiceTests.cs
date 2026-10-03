using System.Text.Json.Nodes;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Knowledge.Validation;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class WorkflowExecutionServiceTests
{
    private readonly KnowledgePackLoader _loader = new();
    private readonly WorkflowExecutionService _service = new();

    [Fact]
    public void Start_selects_exact_workflow_and_snapshots_knowledge_and_source_identity()
    {
        var pack = LoadPack();

        var execution = _service.Start(
            pack,
            "synthetic.review-flow",
            workflowVersion: 2);

        Assert.Equal("synthetic.demo-a", execution.KnowledgePackId);
        Assert.Equal("demo-a-2026.1", execution.KnowledgeRelease);
        Assert.Equal("synthetic.review-flow", execution.Definition.Id);
        Assert.Equal(2, execution.Definition.Version);
        Assert.Equal("submitted", execution.Instance.StateId);
        Assert.Equal(0, execution.Instance.Revision);

        Assert.Equal("SYNTH-DEMO-A-001", execution.Source.Id);
        Assert.Equal(
            "Synthetic Demo A rule source",
            execution.Source.Title);
        Assert.Equal(
            "repository:knowledge/demo-a/pack.json",
            execution.Source.SourceLocation);
    }

    [Fact]
    public void Apply_returns_a_new_execution_and_preserves_the_previous_revision()
    {
        var started = _service.Start(
            LoadPack(),
            "synthetic.review-flow",
            2);

        var reviewed = _service.Apply(started, "request_review");
        var completed = _service.Apply(reviewed, "complete_review");

        Assert.Equal("submitted", started.Instance.StateId);
        Assert.Equal(0, started.Instance.Revision);

        Assert.Equal("review", reviewed.Instance.StateId);
        Assert.Equal(1, reviewed.Instance.Revision);

        Assert.Equal("complete", completed.Instance.StateId);
        Assert.Equal(2, completed.Instance.Revision);
        Assert.True(completed.Instance.IsTerminal(completed.Definition));

        Assert.Same(started.Definition, reviewed.Definition);
        Assert.Same(reviewed.Definition, completed.Definition);
        Assert.Equal(started.Source, completed.Source);
        Assert.Equal(started.KnowledgeRelease, completed.KnowledgeRelease);
    }

    [Fact]
    public void Caller_mutation_after_start_cannot_change_an_in_flight_execution()
    {
        var pack = LoadPack();
        var started = _service.Start(
            pack,
            "synthetic.review-flow",
            2);

        pack.Workflows.Single().States.Clear();
        pack.Workflows.Single().Transitions.Clear();
        pack.Sources[0] = new SourceDefinition
        {
            Id = "SYNTH-DEMO-A-001",
            Authority = "mutated",
            Title = "mutated",
            DocumentType = "SYNTHETIC",
            Status = "ACTIVE",
            Version = "changed",
            SourceLocation = "mutated"
        };

        var reviewed = _service.Apply(started, "request_review");

        Assert.Equal("review", reviewed.Instance.StateId);
        Assert.Equal(
            "Synthetic Demo A rule source",
            reviewed.Source.Title);
        Assert.Equal("1", reviewed.Source.Version);
        Assert.Equal(3, reviewed.Definition.States.Count);
        Assert.Equal(2, reviewed.Definition.Transitions.Count);
    }

    [Theory]
    [InlineData("missing-flow", 2)]
    [InlineData("synthetic.review-flow", 999)]
    public void Unknown_workflow_identity_fails_closed(
        string workflowId,
        int version)
    {
        Assert.Throws<ArgumentException>(
            () => _service.Start(
                LoadPack(),
                workflowId,
                version));
    }

    [Fact]
    public void Invalid_pack_is_rejected_before_workflow_selection()
    {
        var pack = LoadPack();
        var original = pack.Workflows.Single();
        pack.Workflows[0] = new KnowledgeWorkflowDefinition
        {
            Id = original.Id,
            Version = original.Version,
            SourceId = "missing-source",
            InitialStateId = original.InitialStateId,
            States = original.States.ToList(),
            Transitions = original.Transitions.ToList()
        };

        var exception = Assert.Throws<KnowledgeValidationException>(
            () => _service.Start(
                pack,
                "synthetic.review-flow",
                2));

        Assert.Contains(
            exception.Errors,
            error => error.Code == "missing_workflow_source");
    }

    [Fact]
    public void Domain_fail_closed_transition_behavior_is_preserved()
    {
        var started = _service.Start(
            LoadPack(),
            "synthetic.review-flow",
            2);

        Assert.Throws<WorkflowTransitionNotAllowedException>(
            () => _service.Apply(started, "complete_review"));

        var completed = _service.Apply(
            _service.Apply(started, "request_review"),
            "complete_review");

        Assert.Throws<WorkflowTransitionNotAllowedException>(
            () => _service.Apply(completed, "request_review"));
    }

    private KnowledgePack LoadPack()
    {
        var node = (JsonNode.Parse(
            File.ReadAllText(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "demo-a-pack.json"))) as JsonObject)!;

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

        return _loader.LoadFromJson(node.ToJsonString());
    }
}
