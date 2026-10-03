using System.Text.Json;
using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Serialization.Tests;

public sealed class CaseReviewStateJsonTests
{
    private static readonly WorkflowDefinition Workflow = new("synthetic-json-process", 1, "pending", [new("pending", false)], []);
    [Fact]
    public void State_roundtrip_preserves_exact_original_assessment_and_input_revision()
    {
        var state = Initial();
        var json = CaseReviewStateJson.Serialize(state);
        var restored = CaseReviewStateJson.Deserialize(json, (_, _) => Workflow);
        Assert.Equal(json, CaseReviewStateJson.Serialize(restored));
        Assert.Equal(AssessmentRecordJson.Serialize(state.Assessment), AssessmentRecordJson.Serialize(restored.Assessment));
        Assert.Equal(123456789.1234567890123456789m, restored.Assessment.Input.Facts["score"].Number);
        Assert.Equal(3, restored.AssessmentCaseRevision);
    }
    [Theory]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":2")]
    [InlineData("\"caseRevision\":3", "\"caseRevision\":4")]
    [InlineData("\"caseId\":\"case-json-review\"", "\"caseId\":\"other-case\"")]
    [InlineData("\"processRevision\":0", "\"processRevision\":-1")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"formatVersion\":1")]
    [InlineData("\"formatVersion\":1", "\"formatVersion\":1,\"unexpected\":true")]
    public void Invalid_or_ambiguous_state_fails_closed(string from, string to)
        => Assert.Throws<JsonException>(() => CaseReviewStateJson.Deserialize(CaseReviewStateJson.Serialize(Initial()).Replace(from, to), (_, _) => Workflow));
    [Fact]
    public void Missing_properties_and_wrong_workflow_are_rejected()
    {
        Assert.Throws<JsonException>(() => CaseReviewStateJson.Deserialize("{}", (_, _) => Workflow));
        var wrong = new WorkflowDefinition("other", 1, "pending", [new("pending", false)], []);
        Assert.Throws<JsonException>(() => CaseReviewStateJson.Deserialize(CaseReviewStateJson.Serialize(Initial()), (_, _) => wrong));
    }
    private static CaseReviewState Initial()
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-b-pack.json"));
        var time = new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
        var record = new AssessmentRecorder().Evaluate(pack, new Dictionary<string, CaseValue> { ["score"] = 123456789.1234567890123456789m },
            new DateOnly(2026, 10, 3), null, new(new("assessment-json-review"), new("case-json-review"), "synthetic-platform", time));
        return new(record, 3, CaseProcessingInstance.Start(record.CaseId, 3, Workflow),
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, time, "synthetic-ingest")));
    }
}
