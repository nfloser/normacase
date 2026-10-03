using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Application.Workflows;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class WorkflowExecutionSnapshotJsonTests
{
    [Fact]
    public void Complete_workflow_snapshot_roundtrips_losslessly()
    {
        var snapshot = Sample();
        var json = WorkflowExecutionSnapshotJson.Serialize(snapshot);

        var restored = WorkflowExecutionSnapshotJson.Deserialize(json);

        Assert.Equal(json, WorkflowExecutionSnapshotJson.Serialize(restored));
        Assert.Equal("synthetic.pack", restored.KnowledgePackId);
        Assert.Equal("release-2026.10", restored.KnowledgeRelease);
        Assert.Equal("synthetic.review-flow", restored.WorkflowId);
        Assert.Equal(3, restored.WorkflowVersion);
        Assert.Equal("review", restored.StateId);
        Assert.Equal(7, restored.Revision);
        Assert.Equal(3, restored.States.Count);
        Assert.Equal(2, restored.Transitions.Count);
        Assert.Equal("Synthetic workflow source", restored.Source.Title);
        Assert.Equal(
            "sha256:" + new string('a', 64),
            restored.Source.ContentHash);
    }

    [Fact]
    public void Serialize_rejects_a_semantically_invalid_snapshot()
    {
        var sample = Sample();
        var invalid = new WorkflowExecutionSnapshot(
            sample.KnowledgePackId,
            sample.KnowledgeRelease,
            sample.Source,
            sample.WorkflowId,
            sample.WorkflowVersion,
            sample.InitialStateId,
            sample.States,
            sample.Transitions,
            "missing-state",
            sample.Revision);

        Assert.Throws<ArgumentException>(
            () => WorkflowExecutionSnapshotJson.Serialize(invalid));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("state")]
    [InlineData("revision")]
    [InlineData("workflow-version")]
    [InlineData("empty-states")]
    [InlineData("transition")]
    [InlineData("source-title")]
    [InlineData("source-validity")]
    [InlineData("source-hash")]
    [InlineData("missing-optional")]
    [InlineData("null-snapshot")]
    [InlineData("null-source")]
    [InlineData("null-states")]
    [InlineData("null-transitions")]
    [InlineData("null-state")]
    [InlineData("null-transition")]
    public void Malformed_or_semantically_invalid_documents_are_rejected(
        string mutation)
    {
        var node = JsonNode.Parse(
            WorkflowExecutionSnapshotJson.Serialize(Sample()))!;
        var snapshot = node["snapshot"]!;
        var source = snapshot["source"]!;

        switch (mutation)
        {
            case "version":
                node["formatVersion"] = 2;
                break;
            case "unknown":
                snapshot["extra"] = true;
                break;
            case "missing":
                snapshot.AsObject().Remove("knowledgeRelease");
                break;
            case "state":
                snapshot["stateId"] = "missing";
                break;
            case "revision":
                snapshot["revision"] = -1;
                break;
            case "workflow-version":
                snapshot["workflowVersion"] = 0;
                break;
            case "empty-states":
                snapshot["states"] = new JsonArray();
                break;
            case "transition":
                snapshot["transitions"]![0]!["toStateId"] = "missing";
                break;
            case "source-title":
                source["title"] = " ";
                break;
            case "source-validity":
                source["validFrom"] = "2026-10-04";
                source["validUntil"] = "2026-10-03";
                break;
            case "source-hash":
                source["contentHash"] = "sha256:not-a-hash";
                break;
            case "missing-optional":
                source.AsObject().Remove("version");
                break;
            case "null-snapshot":
                node["snapshot"] = null;
                break;
            case "null-source":
                snapshot["source"] = null;
                break;
            case "null-states":
                snapshot["states"] = null;
                break;
            case "null-transitions":
                snapshot["transitions"] = null;
                break;
            case "null-state":
                snapshot["states"]![0] = null;
                break;
            case "null-transition":
                snapshot["transitions"]![0] = null;
                break;
        }

        Assert.Throws<JsonException>(
            () => WorkflowExecutionSnapshotJson.Deserialize(
                node.ToJsonString()));
    }

    [Fact]
    public void Duplicate_properties_are_rejected()
    {
        var json = WorkflowExecutionSnapshotJson.Serialize(Sample());

        Assert.Throws<JsonException>(
            () => WorkflowExecutionSnapshotJson.Deserialize(
                json.Replace(
                    "\"formatVersion\":1",
                    "\"formatVersion\":1,\"formatVersion\":1",
                    StringComparison.Ordinal)));
    }

    private static WorkflowExecutionSnapshot Sample()
        => new(
            "synthetic.pack",
            "release-2026.10",
            new WorkflowSourceSnapshot(
                "SYNTH-WORKFLOW-001",
                "Synthetic authority",
                "Synthetic workflow source",
                "SYNTHETIC",
                "ACTIVE",
                "1",
                "repository:knowledge/synthetic/workflow.json",
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 1),
                new DateOnly(2027, 8, 31),
                new DateOnly(2026, 10, 1),
                "sha256:" + new string('a', 64)),
            "synthetic.review-flow",
            3,
            "submitted",
            [
                new WorkflowStateSnapshot("submitted", false),
                new WorkflowStateSnapshot("review", false),
                new WorkflowStateSnapshot("complete", true)
            ],
            [
                new WorkflowTransitionSnapshot(
                    "request_review",
                    "submitted",
                    "review"),
                new WorkflowTransitionSnapshot(
                    "complete_review",
                    "review",
                    "complete")
            ],
            "review",
            7);
}
