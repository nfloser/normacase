using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class CaseProcessingInstanceJsonTests
{
    [Fact]
    public void Roundtrip_preserves_exact_process_identity_and_revisions()
    {
        var definition = new WorkflowDefinition(
            "synthetic.review-flow",
            4,
            "received",
            [new("received", false), new("awaiting-review", false)],
            [new("route-review", "received", "awaiting-review")]);
        var process = CaseProcessingInstance.Start(
                new CaseId("case-process-json"),
                9,
                definition)
            .Apply(definition, 0, "route-review");

        var json = CaseProcessingInstanceJson.Serialize(process);
        var restored = CaseProcessingInstanceJson.Deserialize(json);

        Assert.Equal(process.CaseId, restored.CaseId);
        Assert.Equal(process.CaseRevision, restored.CaseRevision);
        Assert.Equal(process.WorkflowId, restored.WorkflowId);
        Assert.Equal(process.WorkflowVersion, restored.WorkflowVersion);
        Assert.Equal(process.StateId, restored.StateId);
        Assert.Equal(process.Revision, restored.Revision);
        Assert.Equal(json, CaseProcessingInstanceJson.Serialize(restored));
    }

    [Theory]
    [InlineData("{"formatVersion":2,"caseId":"case","caseRevision":1,"workflowId":"flow","workflowVersion":1,"stateId":"state","processRevision":0}")]
    [InlineData("{"formatVersion":1,"caseId":"case","caseRevision":0,"workflowId":"flow","workflowVersion":1,"stateId":"state","processRevision":0}")]
    [InlineData("{"formatVersion":1,"caseId":"case","caseRevision":1,"workflowId":"flow","workflowVersion":0,"stateId":"state","processRevision":0}")]
    [InlineData("{"formatVersion":1,"caseId":"case","caseRevision":1,"workflowId":"flow","workflowVersion":1,"stateId":"state","processRevision":-1}")]
    public void Invalid_document_is_rejected(string json)
        => Assert.Throws<JsonException>(
            () => CaseProcessingInstanceJson.Deserialize(json));

    [Fact]
    public void Duplicate_and_unknown_properties_are_rejected()
    {
        const string duplicate =
            "{"formatVersion":1,"formatVersion":1,"caseId":"case","caseRevision":1,"workflowId":"flow","workflowVersion":1,"stateId":"state","processRevision":0}";
        const string unknown =
            "{"formatVersion":1,"caseId":"case","caseRevision":1,"workflowId":"flow","workflowVersion":1,"stateId":"state","processRevision":0,"extra":true}";

        Assert.Throws<JsonException>(
            () => CaseProcessingInstanceJson.Deserialize(duplicate));
        Assert.Throws<JsonException>(
            () => CaseProcessingInstanceJson.Deserialize(unknown));
    }
}
