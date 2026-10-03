using System.Text.Json;
using System.Text.Json.Nodes;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class WorkflowRunRecordJsonTests
{
    [Fact]
    public void Whole_history_roundtrips_and_continues_without_current_knowledge()
    {
        var json = WorkflowRunRecordJson.Serialize(CreateRun());
        var restored = WorkflowRunRecordJson.Deserialize(json);
        Assert.Equal(json, WorkflowRunRecordJson.Serialize(restored));
        Assert.Contains("\"runId\":\"run-json\"", json, StringComparison.Ordinal);
        Assert.Contains("\"caseId\":\"case-json\"", json, StringComparison.Ordinal);
        Assert.Equal(2, restored.History.Count);
        Assert.Equal(1, restored.Current.Revision);
        Assert.Equal(7, restored.History[0].RecordedAtUtc.Ticks % 10);
        var completed = new WorkflowRunService().Apply(restored, 1, "finish", "reviewer",
            restored.History[^1].RecordedAtUtc.AddMinutes(1), "Synthetische Prüfung beendet");
        Assert.Equal("done", completed.Current.StateId);
        Assert.Equal(3, completed.History.Count);
    }

    [Theory]
    [InlineData("runId", "null")]
    [InlineData("runId", "123")]
    [InlineData("runId", "\" \"")]
    [InlineData("caseId", "{}")]
    [InlineData("caseId", "\"\"")]
    [InlineData("history", "[]")]
    [InlineData("history", "[null]")]
    public void Invalid_record_values_fail_closed(string property, string token)
    {
        var node = JsonNode.Parse(WorkflowRunRecordJson.Serialize(CreateRun()))!;
        node["run"]![property] = JsonNode.Parse(token);
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(node.ToJsonString()));
    }

    [Theory]
    [InlineData("knowledgeRelease", "\"substituted\"")]
    [InlineData("stateId", "\"done\"")]
    [InlineData("revision", "9")]
    [InlineData("workflowVersion", "2")]
    [InlineData("initialStateId", "\"reviewing\"")]
    public void Substituted_historical_snapshot_is_rejected(string property, string token)
    {
        var node = JsonNode.Parse(WorkflowRunRecordJson.Serialize(CreateRun()))!;
        node["run"]!["history"]![1]!["snapshot"]![property] = JsonNode.Parse(token);
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(node.ToJsonString()));
    }

    [Fact]
    public void Source_graph_event_and_time_corruption_is_rejected_without_echoing_content()
    {
        var json = WorkflowRunRecordJson.Serialize(CreateRun());
        Action<JsonNode>[] mutations =
        [
            node => node["run"]!["history"]![1]!["snapshot"]!["source"]!["title"] = "substituted-sensitive-title",
            node => node["run"]!["history"]![1]!["snapshot"]!["transitions"]![0]!["toStateId"] = "done",
            node => node["run"]!["history"]![1]!["transitionId"] = "finish",
            node => node["run"]!["history"]![1]!["transitionId"] = null,
            node => node["run"]!["history"]![0]!["transitionId"] = "review",
            node => node["run"]!["history"]![1]!["recordedAtUtc"] = "2020-01-01T00:00:00Z",
            node => node["run"]!["history"]![0]!["recordedAtUtc"] = "0001-01-01T00:00:00Z",
            node => node["run"]!["history"]![1]!["actorId"] = " ",
            node => node["run"]!["history"]![1]!["reason"] = " "
        ];
        foreach (var mutate in mutations)
        {
            var node = JsonNode.Parse(json)!;
            mutate(node);
            var exception = Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(node.ToJsonString()));
            Assert.DoesNotContain("substituted-sensitive-title", exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Strict_document_shape_and_version_are_required()
    {
        var json = WorkflowRunRecordJson.Serialize(CreateRun());
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(json.Replace("\"formatVersion\":1", "\"formatVersion\":2")));
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(json.Replace("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")));
        var node = JsonNode.Parse(json)!;
        node["run"]!["unexpected"] = true;
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(node.ToJsonString()));
        node["run"]!.AsObject().Remove("unexpected");
        node["run"]!.AsObject().Remove("caseId");
        Assert.Throws<JsonException>(() => WorkflowRunRecordJson.Deserialize(node.ToJsonString()));
    }

    private static WorkflowRunRecord CreateRun()
    {
        var execution = new WorkflowExecutionService().Restore(new(
            "synthetic.pack", "synthetic.release",
            new WorkflowSourceSnapshot("source", "synthetic", "Synthetic source", "SYNTHETIC", "ACTIVE", "1", null, null, null, null, null, null),
            "synthetic.flow", 1, "submitted",
            [new("submitted", false), new("reviewing", false), new("done", true)],
            [new("review", "submitted", "reviewing"), new("finish", "reviewing", "done")], "submitted", 0));
        var time = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
        var service = new WorkflowRunService();
        var run = service.Start(execution, new WorkflowRunId("run-json"), new CaseId("case-json"), "platform-test", "creator", time, "Synthetischer Start");
        return service.Apply(run, 0, "review", "reviewer", time.AddMinutes(1), "Synthetische Prüfung");
    }
}
