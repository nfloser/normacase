using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class CaseProcessingJsonTests
{
    [Fact]
    public void Roundtrip_preserves_case_revision_process_revision_and_exact_workflow_graph()
    {
        var definition = Definition();
        var process = CaseProcessingInstance
            .Start(new CaseId("case-processing-json"), 7, definition)
            .Apply(definition, 0, "review");

        var json = CaseProcessingJson.Serialize(process, definition);
        var restored = CaseProcessingJson.Deserialize(json);

        Assert.Equal("case-processing-json", restored.Process.CaseId.Value);
        Assert.Equal(7, restored.Process.CaseRevision);
        Assert.Equal("synthetic-process", restored.Process.WorkflowId);
        Assert.Equal(3, restored.Process.WorkflowVersion);
        Assert.Equal("reviewing", restored.Process.StateId);
        Assert.Equal(1, restored.Process.Revision);
        Assert.Equal(definition.InitialStateId, restored.Definition.InitialStateId);
        Assert.Equal(
            definition.States.Select(state => (state.Id, state.IsTerminal)),
            restored.Definition.States.Select(state => (state.Id, state.IsTerminal)));
        Assert.Equal(
            definition.Transitions.Select(transition =>
                (transition.Id, transition.FromStateId, transition.ToStateId)),
            restored.Definition.Transitions.Select(transition =>
                (transition.Id, transition.FromStateId, transition.ToStateId)));

        Assert.Equal(
            json,
            CaseProcessingJson.Serialize(
                restored.Process,
                restored.Definition));
    }

    [Fact]
    public void Serializer_rejects_a_definition_that_does_not_match_the_process()
    {
        var process = CaseProcessingInstance.Start(
            new CaseId("case-processing-json"),
            1,
            Definition());

        var other = new WorkflowDefinition(
            "other-process",
            1,
            "start",
            [new("start", false)],
            []);

        Assert.Throws<ArgumentException>(
            () => CaseProcessingJson.Serialize(process, other));
    }

    [Fact]
    public void Import_rejects_unknown_properties_invalid_state_and_unsupported_version()
    {
        var definition = Definition();
        var process = CaseProcessingInstance.Start(
            new CaseId("case-processing-json"),
            1,
            definition);
        var json = CaseProcessingJson.Serialize(process, definition);

        Assert.Throws<JsonException>(
            () => CaseProcessingJson.Deserialize(
                json.Replace(
                    "\"formatVersion\":1",
                    "\"formatVersion\":1,\"unexpected\":true",
                    StringComparison.Ordinal)));

        Assert.Throws<JsonException>(
            () => CaseProcessingJson.Deserialize(
                json.Replace(
                    "\"stateId\":\"received\"",
                    "\"stateId\":\"missing\"",
                    StringComparison.Ordinal)));

        Assert.Throws<JsonException>(
            () => CaseProcessingJson.Deserialize(
                json.Replace(
                    "\"formatVersion\":1",
                    "\"formatVersion\":2",
                    StringComparison.Ordinal)));
    }

    private static WorkflowDefinition Definition()
        => new(
            "synthetic-process",
            3,
            "received",
            [
                new("received", false),
                new("reviewing", false),
                new("done", true)
            ],
            [
                new("review", "received", "reviewing"),
                new("finish", "reviewing", "done")
            ]);
}
