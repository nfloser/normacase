using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Outbound;

internal static class OutboundBoundary
{
    internal static void Identifier(string value, string parameter)
    {
        if (value is null || value.Length > 128
            || !Regex.IsMatch(value, "\\A[A-Za-z0-9][A-Za-z0-9._@:-]*\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid outbound identifier.", parameter);
    }
}

public sealed record OutboundResultCommand
{
    public OutboundResultCommand(string messageId, string correlationId, long expectedCaseRevision,
        long expectedProcessRevision, long expectedAuditRevision)
    {
        OutboundBoundary.Identifier(messageId, nameof(messageId));
        OutboundBoundary.Identifier(correlationId, nameof(correlationId));
        if (expectedCaseRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedCaseRevision));
        if (expectedProcessRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedProcessRevision));
        if (expectedAuditRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedAuditRevision));
        MessageId = messageId;
        CorrelationId = correlationId;
        ExpectedCaseRevision = expectedCaseRevision;
        ExpectedProcessRevision = expectedProcessRevision;
        ExpectedAuditRevision = expectedAuditRevision;
    }

    public string MessageId { get; }
    public string CorrelationId { get; }
    public long ExpectedCaseRevision { get; }
    public long ExpectedProcessRevision { get; }
    public long ExpectedAuditRevision { get; }
}

public sealed class ReviewedOutboundResult
{
    public ReviewedOutboundResult(string messageId, string correlationId, CaseId caseId, long caseRevision,
        string caseTypeId, AssessmentId assessmentId, DateTimeOffset assessmentRecordedAtUtc,
        string knowledgePackId, string knowledgeRelease, string platformVersion,
        string workflowId, int workflowVersion, string stateId, long processRevision, long auditRevision,
        ReviewId reviewId, string reviewActorId, DateTimeOffset reviewRecordedAtUtc,
        HumanReviewDisposition disposition, string reviewReason, AssessmentOutcome originalOutcome,
        AssessmentOutcome humanOutcome, ReviewReference? reviewReference, IntakeProvenance provenance,
        AssessmentInputSnapshot input, IReadOnlyDictionary<string, IReadOnlyList<string>> evidenceReferences)
    {
        OutboundBoundary.Identifier(messageId, nameof(messageId));
        OutboundBoundary.Identifier(correlationId, nameof(correlationId));
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case id required.", nameof(caseId));
        OutboundBoundary.Identifier(caseTypeId, nameof(caseTypeId));
        if (assessmentId.IsEmpty) throw new ArgumentException("Explicit assessment id required.", nameof(assessmentId));
        if (reviewId.IsEmpty) throw new ArgumentException("Explicit review id required.", nameof(reviewId));
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRelease);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewReason);
        if (caseRevision < 1 || processRevision < 0 || auditRevision < 2)
            throw new ArgumentOutOfRangeException(nameof(caseRevision));
        if (workflowVersion < 1) throw new ArgumentOutOfRangeException(nameof(workflowVersion));
        if (assessmentRecordedAtUtc == default || assessmentRecordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Assessment UTC timestamp required.", nameof(assessmentRecordedAtUtc));
        if (reviewRecordedAtUtc == default || reviewRecordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Review UTC timestamp required.", nameof(reviewRecordedAtUtc));
        if (!Enum.IsDefined(disposition) || !Enum.IsDefined(originalOutcome) || !Enum.IsDefined(humanOutcome))
            throw new ArgumentException("Invalid outbound decision value.");
        if (disposition == HumanReviewDisposition.AcceptSystemResult && humanOutcome != originalOutcome)
            throw new ArgumentException("Accepted review must preserve the system outcome.", nameof(humanOutcome));
        if (disposition == HumanReviewDisposition.Override && humanOutcome == originalOutcome)
            throw new ArgumentException("Override must carry its explicit human outcome.", nameof(humanOutcome));
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(evidenceReferences);

        var references = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var item in evidenceReferences)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item.Key);
            if (item.Value is null) throw new ArgumentException("Evidence references cannot be null.", nameof(evidenceReferences));
            references.Add(item.Key, Array.AsReadOnly(item.Value.ToArray()));
        }

        MessageId = messageId; CorrelationId = correlationId; CaseId = caseId; CaseRevision = caseRevision;
        CaseTypeId = caseTypeId; AssessmentId = assessmentId; AssessmentRecordedAtUtc = assessmentRecordedAtUtc;
        KnowledgePackId = knowledgePackId; KnowledgeRelease = knowledgeRelease; PlatformVersion = platformVersion;
        WorkflowId = workflowId; WorkflowVersion = workflowVersion; StateId = stateId; ProcessRevision = processRevision;
        AuditRevision = auditRevision; ReviewId = reviewId; ReviewActorId = reviewActorId;
        ReviewRecordedAtUtc = reviewRecordedAtUtc; Disposition = disposition; ReviewReason = reviewReason;
        OriginalOutcome = originalOutcome; HumanOutcome = humanOutcome; ReviewReference = reviewReference;
        Provenance = provenance;
        Input = new AssessmentInputSnapshot(input.AssessmentDate, input.Facts, input.Evidence);
        EvidenceReferences = new ReadOnlyDictionary<string, IReadOnlyList<string>>(references);
    }

    public string MessageId { get; }
    public string CorrelationId { get; }
    public CaseId CaseId { get; }
    public long CaseRevision { get; }
    public string CaseTypeId { get; }
    public AssessmentId AssessmentId { get; }
    public DateTimeOffset AssessmentRecordedAtUtc { get; }
    public string KnowledgePackId { get; }
    public string KnowledgeRelease { get; }
    public string PlatformVersion { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public string StateId { get; }
    public long ProcessRevision { get; }
    public long AuditRevision { get; }
    public ReviewId ReviewId { get; }
    public string ReviewActorId { get; }
    public DateTimeOffset ReviewRecordedAtUtc { get; }
    public HumanReviewDisposition Disposition { get; }
    public string ReviewReason { get; }
    public AssessmentOutcome OriginalOutcome { get; }
    public AssessmentOutcome HumanOutcome { get; }
    public ReviewReference? ReviewReference { get; }
    public IntakeProvenance Provenance { get; }
    public AssessmentInputSnapshot Input { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> EvidenceReferences { get; }
}

public sealed class ReviewedOutboundResultBuilder
{
    public ReviewedOutboundResult Build(NormalizedIntakeRecord intake, CaseReviewState state,
        WorkflowDefinition workflow, OutboundResultCommand command)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(command);

        if (intake.CaseId != state.Assessment.CaseId || state.Process.CaseId != state.Assessment.CaseId
            || state.Audit.AssessmentId != state.Assessment.AssessmentId
            || intake.KnowledgePackId != state.Assessment.KnowledgePackId
            || intake.KnowledgeRelease != state.Assessment.Result.KnowledgeRelease
            || !SameInput(intake.Input, state.Assessment.Input))
            throw new OutboundResultBindingException();

        if (state.Process.CaseRevision != command.ExpectedCaseRevision
            || state.Process.Revision != command.ExpectedProcessRevision
            || state.Audit.Events[^1].Sequence != command.ExpectedAuditRevision)
            throw new OutboundResultConflictException();

        if (state.Process.WorkflowId != workflow.Id || state.Process.WorkflowVersion != workflow.Version
            || !state.Process.IsTerminal(workflow))
            throw new OutboundResultNotReadyException();

        var last = state.Audit.Events[^1];
        if (last.Kind != AuditEventKind.HumanReviewRecorded || last.Review is null
            || last.Review.AssessmentId != state.Assessment.AssessmentId)
            throw new OutboundResultNotReadyException();

        var review = last.Review;
        var humanOutcome = review.Disposition == HumanReviewDisposition.Override
            ? review.OverrideOutcome!.Value
            : state.Assessment.Result.Outcome;

        return new ReviewedOutboundResult(command.MessageId, command.CorrelationId,
            state.Assessment.CaseId, state.Process.CaseRevision, intake.CaseTypeId,
            state.Assessment.AssessmentId, state.Assessment.RecordedAtUtc,
            state.Assessment.KnowledgePackId, state.Assessment.Result.KnowledgeRelease,
            state.Assessment.PlatformVersion, state.Process.WorkflowId, state.Process.WorkflowVersion,
            state.Process.StateId, state.Process.Revision, last.Sequence, review.ReviewId,
            review.ActorId, review.RecordedAt, review.Disposition, review.Reason,
            state.Assessment.Result.Outcome, humanOutcome, review.Reference, intake.Provenance,
            state.Assessment.Input, intake.EvidenceReferences);
    }

    private static bool SameInput(AssessmentInputSnapshot left, AssessmentInputSnapshot right)
        => left.AssessmentDate == right.AssessmentDate
            && Same(left.Facts, right.Facts) && Same(left.Evidence, right.Evidence);

    private static bool Same<T>(IReadOnlyDictionary<string,T> left, IReadOnlyDictionary<string,T> right)
        => left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out var value)
            && EqualityComparer<T>.Default.Equals(item.Value, value));
}

public sealed class OutboundResultBindingException : InvalidOperationException
{
    public OutboundResultBindingException() : base("Outbound result does not match the recorded intake and assessment.") { }
}

public sealed class OutboundResultConflictException : InvalidOperationException
{
    public OutboundResultConflictException() : base("Outbound result revisions are stale.") { }
}

public sealed class OutboundResultNotReadyException : InvalidOperationException
{
    public OutboundResultNotReadyException() : base("Outbound result requires a reviewed terminal case state.") { }
}
