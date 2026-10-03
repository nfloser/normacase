using NormaCase.Application.Intake;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Outbound;

// A bounded transport-neutral result, not a delivery receipt or authorization proof.
public sealed record ReviewedCaseResult(
    string MessageId, string CorrelationId, string SourceSystemId, string UpstreamCaseId,
    string UpstreamMessageId, long UpstreamRevision, string CaseId, long CaseRevision,
    string AssessmentId, string PlatformVersion, string KnowledgePackId, string KnowledgeRelease,
    DateOnly AssessmentDate, string WorkflowId, int WorkflowVersion, string StateId,
    long ProcessRevision, long AuditRevision, string ReviewId, DateTimeOffset ReviewedAtUtc,
    HumanReviewDisposition ReviewDisposition, AssessmentOutcome OriginalOutcome, AssessmentOutcome HumanOutcome)
{
    public void Validate()
    {
        foreach(var id in new[]{MessageId,CorrelationId,SourceSystemId,UpstreamCaseId,UpstreamMessageId,
            CaseId})
            IntakeBoundary.Identifier(id);
        foreach(var metadata in new[]{AssessmentId,PlatformVersion,KnowledgePackId,KnowledgeRelease,WorkflowId,StateId,ReviewId})
            if(string.IsNullOrWhiteSpace(metadata)||metadata.Length>256||metadata.Any(char.IsControl))
                throw new ArgumentException("Explicit bounded outbound metadata required.");
        if(UpstreamRevision<1||CaseRevision<1||ProcessRevision<1||AuditRevision<2||WorkflowVersion<1)
            throw new ArgumentException("Invalid reviewed outbound revisions.");
        if(AssessmentDate==default||ReviewedAtUtc==default||ReviewedAtUtc.Offset!=TimeSpan.Zero)
            throw new ArgumentException("Explicit assessment date and UTC review time required.");
        if(!Enum.IsDefined(ReviewDisposition)||!Enum.IsDefined(OriginalOutcome)||!Enum.IsDefined(HumanOutcome)
            ||ReviewDisposition==HumanReviewDisposition.AcceptSystemResult&&OriginalOutcome!=HumanOutcome)
            throw new ArgumentException("Invalid reviewed outbound outcome binding.");
    }
}
public sealed record OutboundExpectedRevisions(long CaseRevision,long ProcessRevision,long AuditRevision);
public sealed class OutboundResultBindingException : InvalidOperationException
{
    public OutboundResultBindingException():base("Outbound result does not match a reviewed case and intake."){}
}
public sealed class OutboundResultConflictException : InvalidOperationException
{
    public OutboundResultConflictException():base("Outbound result revisions are stale."){}
}

public sealed class ReviewedCaseResultFactory
{
    // The host must load authoritative immutable intake/review state and authorize export.
    // This operation performs no storage lookup, clock read, state change or delivery.
    public ReviewedCaseResult Create(NormalizedIntakeRecord intake,CaseReviewState state,
        WorkflowDefinition workflow,OutboundExpectedRevisions expected,string messageId,string correlationId)
    {
        ArgumentNullException.ThrowIfNull(intake);ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(workflow);ArgumentNullException.ThrowIfNull(expected);
        if(expected.CaseRevision<1||expected.ProcessRevision<0||expected.AuditRevision<1)
            throw new ArgumentOutOfRangeException(nameof(expected));
        var assessment=state.Assessment;var process=state.Process;var audit=state.Audit;
        if(intake.CaseId!=assessment.CaseId||process.CaseId!=assessment.CaseId
            ||state.AssessmentCaseRevision!=process.CaseRevision||state.AssessmentCaseRevision<1
            ||audit.AssessmentId!=assessment.AssessmentId||audit.Events[0].OccurredAt!=assessment.RecordedAtUtc
            ||intake.KnowledgePackId!=assessment.KnowledgePackId||intake.KnowledgeRelease!=assessment.Result.KnowledgeRelease
            ||intake.Input.AssessmentDate!=assessment.Input.AssessmentDate
            ||!Equal(intake.Input.Facts,assessment.Input.Facts)||!Equal(intake.Input.Evidence,assessment.Input.Evidence))
            throw new OutboundResultBindingException();
        var last=audit.Events[^1];
        if(expected.CaseRevision!=process.CaseRevision||expected.ProcessRevision!=process.Revision||expected.AuditRevision!=last.Sequence)
            throw new OutboundResultConflictException();
        if(process.WorkflowId!=workflow.Id||process.WorkflowVersion!=workflow.Version
            ||!workflow.TryGetState(process.StateId,out var terminal)||!terminal.IsTerminal
            ||last.Kind!=AuditEventKind.HumanReviewRecorded||last.Review is null
            ||last.Review.AssessmentId!=assessment.AssessmentId)
            throw new OutboundResultBindingException();
        var review=last.Review;
        var result=new ReviewedCaseResult(messageId,correlationId,intake.Provenance.SourceSystemId,
            intake.Provenance.UpstreamCaseId,intake.Provenance.MessageId,intake.Provenance.UpstreamRevision,
            process.CaseId.Value,process.CaseRevision,assessment.AssessmentId.Value,assessment.PlatformVersion,
            assessment.KnowledgePackId,assessment.Result.KnowledgeRelease,assessment.Input.AssessmentDate,
            process.WorkflowId,process.WorkflowVersion,process.StateId,process.Revision,last.Sequence,
            review.ReviewId.Value,review.RecordedAt,review.Disposition,assessment.Result.Outcome,
            review.OverrideOutcome??assessment.Result.Outcome);
        result.Validate();return result;
    }
    private static bool Equal<T>(IReadOnlyDictionary<string,T> left,IReadOnlyDictionary<string,T> right)
        =>left.Count==right.Count&&left.All(item=>right.TryGetValue(item.Key,out var value)&&EqualityComparer<T>.Default.Equals(item.Value,value));
}
