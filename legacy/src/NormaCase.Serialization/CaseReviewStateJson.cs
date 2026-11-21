using System.Text.Json;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Serialization;

public sealed record CaseReviewStateDocument(int FormatVersion, string AssessmentJson, long AssessmentCaseRevision,
    string CaseId, long CaseRevision, string WorkflowId, int WorkflowVersion, string StateId, long ProcessRevision, string AuditJson);

public static class CaseReviewStateJson
{
    public static string Serialize(CaseReviewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        return JsonSerializer.Serialize(new CaseReviewStateDocument(1, AssessmentRecordJson.Serialize(state.Assessment),
            state.AssessmentCaseRevision, state.Process.CaseId.Value, state.Process.CaseRevision, state.Process.WorkflowId,
            state.Process.WorkflowVersion, state.Process.StateId, state.Process.Revision, AssessmentAuditJson.Serialize(state.Audit)), InterchangeJson.Options);
    }

    public static CaseReviewState Deserialize(string json, Func<string, int, WorkflowDefinition> resolveWorkflow)
    {
        ArgumentNullException.ThrowIfNull(resolveWorkflow);
        var document = InterchangeJson.Read<CaseReviewStateDocument>(json);
        if (document.FormatVersion != 1) throw new JsonException("Unsupported case review format.");
        try
        {
            var workflow = resolveWorkflow(document.WorkflowId, document.WorkflowVersion);
            if (workflow.Id != document.WorkflowId || workflow.Version != document.WorkflowVersion)
                throw new JsonException("Workflow binding mismatch.");
            var state = new CaseReviewState(AssessmentRecordJson.Deserialize(document.AssessmentJson), document.AssessmentCaseRevision,
                CaseProcessingInstance.Restore(new(document.CaseId), document.CaseRevision, workflow, document.StateId, document.ProcessRevision),
                AssessmentAuditJson.Deserialize(document.AuditJson));
            Validate(state);
            return state;
        }
        catch (ArgumentException) { throw new JsonException("Invalid case review state."); }
    }

    private static void Validate(CaseReviewState state)
    {
        if (state.AssessmentCaseRevision < 1 || state.AssessmentCaseRevision != state.Process.CaseRevision
            || state.Assessment.CaseId != state.Process.CaseId || state.Audit.AssessmentId != state.Assessment.AssessmentId
            || state.Audit.Events[0].OccurredAt != state.Assessment.RecordedAtUtc)
            throw new JsonException("Invalid case review binding.");
    }
}
