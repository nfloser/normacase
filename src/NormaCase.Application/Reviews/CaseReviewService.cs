using NormaCase.Domain.Audit;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Reviews;

public sealed class CaseReviewService
{
    private readonly ICaseReviewTransactionStore store;
    private readonly ICaseReviewAuthorizer authorizer;
    public CaseReviewService(ICaseReviewTransactionStore store, ICaseReviewAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authorizer);
        this.store = store;
        this.authorizer = authorizer;
    }

    public Task<CaseReviewState> ReviewAsync(AuthenticatedReviewActor actor, CaseReviewCommand command,
        WorkflowDefinition workflow, CaseReviewPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy);
        if (command.CaseId.IsEmpty) throw new ArgumentException("Explicit case id required.", nameof(command));
        if (command.ExpectedCaseRevision < 1 || command.ExpectedProcessRevision < 0 || command.ExpectedAuditRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(command));
        // Validation reuses Domain semantics; actor is taken only from the trusted authentication context.
        var review = new HumanReviewRecord(command.ReviewId, command.AssessmentId, actor.ActorId,
            command.RecordedAtUtc, command.Disposition, command.Reason, command.OverrideOutcome, command.Reference);
        return store.ExecuteAsync(command.CaseId, current =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(current);
            if (current.AssessmentCaseRevision < 1 || current.AssessmentCaseRevision != current.Process.CaseRevision
                || current.Assessment.CaseId != command.CaseId || current.Process.CaseId != command.CaseId
                || current.Assessment.AssessmentId != command.AssessmentId || current.Audit.AssessmentId != command.AssessmentId
                || current.Audit.Events[0].OccurredAt != current.Assessment.RecordedAtUtc)
                throw new CaseReviewBindingException();
            if (!authorizer.Authorize(actor, current, command, policy)) throw new CaseReviewDeniedException();
            if (current.Process.CaseRevision != command.ExpectedCaseRevision
                || current.Process.Revision != command.ExpectedProcessRevision
                || current.Audit.Events[^1].Sequence != command.ExpectedAuditRevision
                || current.Audit.Events.Any(item => item.Review?.ReviewId == command.ReviewId))
                throw new CaseReviewConflictException();
            if (current.Process.WorkflowId != workflow.Id || current.Process.WorkflowVersion != workflow.Version
                || policy.WorkflowId != workflow.Id || policy.WorkflowVersion != workflow.Version
                || !policy.TryGetTransition(command.Disposition, out var transition)
                || !workflow.TryGetTransition(transition, out var configured)
                || configured.FromStateId != current.Process.StateId)
                throw new CaseReviewPolicyException();
            var audit = current.Audit.Append(AssessmentAuditEvent.HumanReviewRecorded(
                checked(command.ExpectedAuditRevision + 1), review));
            var process = current.Process.Apply(workflow, command.ExpectedProcessRevision, transition);
            return new(current.Assessment, current.AssessmentCaseRevision, process, audit);
        }, cancellationToken);
    }
}
