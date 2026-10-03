using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Audit;

/// <summary>
/// Identity established by a trusted authentication adapter. Review commands deliberately
/// do not carry an actor id so request payloads cannot choose their audit identity.
/// </summary>
public sealed record AuthenticatedReviewActor
{
    public AuthenticatedReviewActor(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ActorId = actorId;
    }

    public string ActorId { get; }
}

public sealed record CaseReviewAuthorizationRequest(
    CaseId CaseId,
    AssessmentId AssessmentId,
    string WorkflowId,
    int WorkflowVersion,
    HumanReviewDisposition Disposition,
    string PolicyId,
    int PolicyVersion);

public interface ICaseReviewAuthorizer
{
    ValueTask<bool> IsAuthorizedAsync(
        AuthenticatedReviewActor actor,
        CaseReviewAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CaseReviewCommand
{
    public CaseReviewCommand(
        ReviewId reviewId,
        CaseId caseId,
        AssessmentId assessmentId,
        long expectedInputRevision,
        long expectedProcessRevision,
        long expectedAuditSequence,
        string workflowId,
        int workflowVersion,
        HumanReviewDisposition disposition,
        DateTimeOffset recordedAtUtc,
        string reason,
        AssessmentOutcome? overrideOutcome = null,
        ReviewReference? reference = null)
    {
        if (reviewId.IsEmpty)
            throw new ArgumentException("Review id must be explicit.", nameof(reviewId));
        if (caseId.IsEmpty)
            throw new ArgumentException("Case id must be explicit.", nameof(caseId));
        if (assessmentId.IsEmpty)
            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        if (expectedInputRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedInputRevision));
        if (expectedProcessRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedProcessRevision));
        if (expectedAuditSequence < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedAuditSequence));
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        if (workflowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(workflowVersion));
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition));
        if (recordedAtUtc == default || recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "Review timestamp must be an explicit UTC value.",
                nameof(recordedAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (overrideOutcome is not null && !Enum.IsDefined(overrideOutcome.Value))
            throw new ArgumentOutOfRangeException(nameof(overrideOutcome));
        if (disposition == HumanReviewDisposition.AcceptSystemResult
            && overrideOutcome is not null)
            throw new ArgumentException(
                "Accepted reviews cannot carry an override outcome.",
                nameof(overrideOutcome));
        if (disposition == HumanReviewDisposition.Override
            && overrideOutcome is null)
            throw new ArgumentException(
                "Override reviews require an explicit outcome.",
                nameof(overrideOutcome));

        ReviewId = reviewId;
        CaseId = caseId;
        AssessmentId = assessmentId;
        ExpectedInputRevision = expectedInputRevision;
        ExpectedProcessRevision = expectedProcessRevision;
        ExpectedAuditSequence = expectedAuditSequence;
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        Disposition = disposition;
        RecordedAtUtc = recordedAtUtc;
        Reason = reason;
        OverrideOutcome = overrideOutcome;
        Reference = reference;
    }

    public ReviewId ReviewId { get; }
    public CaseId CaseId { get; }
    public AssessmentId AssessmentId { get; }
    public long ExpectedInputRevision { get; }
    public long ExpectedProcessRevision { get; }
    public long ExpectedAuditSequence { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public HumanReviewDisposition Disposition { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public string Reason { get; }
    public AssessmentOutcome? OverrideOutcome { get; }
    public ReviewReference? Reference { get; }
}

public sealed record CaseReviewTransitionBinding(
    string StateId,
    HumanReviewDisposition Disposition,
    string TransitionId);

public sealed class CaseReviewTransitionPolicy
{
    private readonly IReadOnlyDictionary<
        (string StateId, HumanReviewDisposition Disposition),
        string> transitions;

    public CaseReviewTransitionPolicy(
        string id,
        int version,
        WorkflowDefinition definition,
        IEnumerable<CaseReviewTransitionBinding> bindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(bindings);

        var mapped = new Dictionary<
            (string StateId, HumanReviewDisposition Disposition),
            string>();

        foreach (var binding in bindings)
        {
            if (binding is null
                || string.IsNullOrWhiteSpace(binding.StateId)
                || string.IsNullOrWhiteSpace(binding.TransitionId)
                || !Enum.IsDefined(binding.Disposition))
            {
                throw new ArgumentException(
                    "Review transition binding is invalid.",
                    nameof(bindings));
            }

            if (!definition.TryGetTransition(
                    binding.TransitionId,
                    out var transition)
                || !string.Equals(
                    transition.FromStateId,
                    binding.StateId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Review transition must be declared from the configured state.",
                    nameof(bindings));
            }

            if (mapped.Count >= 256
                || !mapped.TryAdd(
                    (binding.StateId, binding.Disposition),
                    binding.TransitionId))
            {
                throw new ArgumentException(
                    "Review transition bindings must be unique and bounded.",
                    nameof(bindings));
            }
        }

        Id = id;
        Version = version;
        Definition = definition;
        transitions = mapped;
    }

    public string Id { get; }
    public int Version { get; }
    public WorkflowDefinition Definition { get; }

    internal string? Find(
        string stateId,
        HumanReviewDisposition disposition)
        => transitions.GetValueOrDefault((stateId, disposition));
}

/// <summary>
/// Snapshot loaded under one transaction/lock. InputRevision identifies the immutable
/// case-input revision to which the process state is bound.
/// </summary>
public sealed record CaseReviewTransactionState
{
    public CaseReviewTransactionState(
        AssessmentRecord assessment,
        long inputRevision,
        CaseProcessingInstance process,
        AssessmentAuditTrail auditTrail)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (inputRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(inputRevision));
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(auditTrail);

        Assessment = assessment;
        InputRevision = inputRevision;
        Process = process;
        AuditTrail = auditTrail;
    }

    public AssessmentRecord Assessment { get; }
    public long InputRevision { get; }
    public CaseProcessingInstance Process { get; }
    public AssessmentAuditTrail AuditTrail { get; }
}

public sealed record CaseReviewTransactionMutation
{
    public CaseReviewTransactionMutation(
        CaseProcessingInstance process,
        AssessmentAuditTrail auditTrail)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(auditTrail);
        Process = process;
        AuditTrail = auditTrail;
    }

    public CaseProcessingInstance Process { get; }
    public AssessmentAuditTrail AuditTrail { get; }
}

/// <summary>
/// Executes the callback against one current case snapshot while holding the store's
/// transaction/lock. If the callback completes, Process and AuditTrail must be committed
/// atomically; if it throws, neither may change. The immutable Assessment is never replaced.
/// </summary>
public interface ICaseReviewTransactionStore
{
    Task<CaseReviewTransactionMutation> ExecuteAsync(
        CaseId caseId,
        Func<
            CaseReviewTransactionState,
            CancellationToken,
            ValueTask<CaseReviewTransactionMutation>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies an authorized human review to audit history and case processing atomically.
/// Authentication is supplied by the caller as AuthenticatedReviewActor; the command
/// itself cannot select an actor.
/// </summary>
public sealed class CaseReviewCommitService
{
    private readonly ICaseReviewTransactionStore store;
    private readonly ICaseReviewAuthorizer authorizer;

    public CaseReviewCommitService(
        ICaseReviewTransactionStore store,
        ICaseReviewAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(authorizer);
        this.store = store;
        this.authorizer = authorizer;
    }

    public Task<CaseReviewTransactionMutation> CommitAsync(
        AuthenticatedReviewActor actor,
        CaseReviewCommand command,
        CaseReviewTransitionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(policy);

        return store.ExecuteAsync(
            command.CaseId,
            async (current, transactionCancellation) =>
            {
                ValidateBinding(current, command, policy);

                var authorization = new CaseReviewAuthorizationRequest(
                    command.CaseId,
                    command.AssessmentId,
                    command.WorkflowId,
                    command.WorkflowVersion,
                    command.Disposition,
                    policy.Id,
                    policy.Version);

                if (!await authorizer.IsAuthorizedAsync(
                        actor,
                        authorization,
                        transactionCancellation))
                {
                    throw new CaseReviewAuthorizationDeniedException();
                }

                ValidateRevisions(current, command);

                if (current.AuditTrail.Events.Any(
                        item => item.Review?.ReviewId == command.ReviewId))
                {
                    throw new CaseReviewDuplicateException();
                }

                var transitionId = policy.Find(
                    current.Process.StateId,
                    command.Disposition);
                if (transitionId is null)
                    throw new CaseReviewTransitionUnavailableException();

                var review = new HumanReviewRecord(
                    command.ReviewId,
                    command.AssessmentId,
                    actor.ActorId,
                    command.RecordedAtUtc,
                    command.Disposition,
                    command.Reason,
                    command.OverrideOutcome,
                    command.Reference);

                var nextAudit = current.AuditTrail.Append(
                    AssessmentAuditEvent.HumanReviewRecorded(
                        checked(current.AuditTrail.Events[^1].Sequence + 1),
                        review));

                var nextProcess = current.Process.Apply(
                    policy.Definition,
                    command.ExpectedProcessRevision,
                    transitionId);

                return new CaseReviewTransactionMutation(
                    nextProcess,
                    nextAudit);
            },
            cancellationToken);
    }

    private static void ValidateBinding(
        CaseReviewTransactionState current,
        CaseReviewCommand command,
        CaseReviewTransitionPolicy policy)
    {
        var assessment = current.Assessment;
        var process = current.Process;
        var audit = current.AuditTrail;
        var created = audit.Events[0];

        if (assessment.CaseId != command.CaseId
            || assessment.AssessmentId != command.AssessmentId
            || process.CaseId != command.CaseId
            || process.CaseRevision != current.InputRevision
            || audit.AssessmentId != command.AssessmentId
            || created.Kind != AuditEventKind.AssessmentCreated
            || created.AssessmentId != assessment.AssessmentId
            || created.OccurredAt != assessment.RecordedAtUtc
            || !string.Equals(
                process.WorkflowId,
                command.WorkflowId,
                StringComparison.Ordinal)
            || process.WorkflowVersion != command.WorkflowVersion
            || !string.Equals(
                policy.Definition.Id,
                command.WorkflowId,
                StringComparison.Ordinal)
            || policy.Definition.Version != command.WorkflowVersion)
        {
            throw new CaseReviewBindingException();
        }
    }

    private static void ValidateRevisions(
        CaseReviewTransactionState current,
        CaseReviewCommand command)
    {
        var actualAuditSequence =
            current.AuditTrail.Events[^1].Sequence;

        if (command.ExpectedInputRevision != current.InputRevision
            || command.ExpectedProcessRevision != current.Process.Revision
            || command.ExpectedAuditSequence != actualAuditSequence)
        {
            throw new CaseReviewConcurrencyException(
                command.ExpectedInputRevision,
                current.InputRevision,
                command.ExpectedProcessRevision,
                current.Process.Revision,
                command.ExpectedAuditSequence,
                actualAuditSequence);
        }
    }
}

public sealed class CaseReviewAuthorizationDeniedException
    : Exception
{
    public CaseReviewAuthorizationDeniedException()
        : base("Actor is not authorized for this case review.") { }
}

public sealed class CaseReviewBindingException
    : Exception
{
    public CaseReviewBindingException()
        : base("Review command is not bound to the stored case, assessment, workflow and audit state.") { }
}

public sealed class CaseReviewDuplicateException
    : Exception
{
    public CaseReviewDuplicateException()
        : base("Review id has already been committed.") { }
}

public sealed class CaseReviewTransitionUnavailableException
    : Exception
{
    public CaseReviewTransitionUnavailableException()
        : base("No configured review transition is available from the current process state.") { }
}

public sealed class CaseReviewConcurrencyException
    : Exception
{
    public CaseReviewConcurrencyException(
        long expectedInputRevision,
        long actualInputRevision,
        long expectedProcessRevision,
        long actualProcessRevision,
        long expectedAuditSequence,
        long actualAuditSequence)
        : base("Review command revisions do not match the current committed case state.")
    {
        ExpectedInputRevision = expectedInputRevision;
        ActualInputRevision = actualInputRevision;
        ExpectedProcessRevision = expectedProcessRevision;
        ActualProcessRevision = actualProcessRevision;
        ExpectedAuditSequence = expectedAuditSequence;
        ActualAuditSequence = actualAuditSequence;
    }

    public long ExpectedInputRevision { get; }
    public long ActualInputRevision { get; }
    public long ExpectedProcessRevision { get; }
    public long ActualProcessRevision { get; }
    public long ExpectedAuditSequence { get; }
    public long ActualAuditSequence { get; }
}
