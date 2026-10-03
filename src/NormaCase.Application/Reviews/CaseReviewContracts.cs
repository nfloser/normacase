using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;

namespace NormaCase.Application.Reviews;

/// <summary>Trusted authentication-adapter output, never an HTTP request DTO.</summary>
public sealed record AuthenticatedReviewActor
{
    public AuthenticatedReviewActor(string actorId, string authenticationAuthority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationAuthority);
        ActorId = actorId;
        AuthenticationAuthority = authenticationAuthority;
    }
    // Must be globally stable/authority-qualified at the adapter boundary.
    public string ActorId { get; }
    public string AuthenticationAuthority { get; }
}

public sealed record CaseReviewCommand(CaseId CaseId, AssessmentId AssessmentId, ReviewId ReviewId,
    long ExpectedCaseRevision, long ExpectedProcessRevision, long ExpectedAuditRevision,
    DateTimeOffset RecordedAtUtc, HumanReviewDisposition Disposition, string Reason,
    AssessmentOutcome? OverrideOutcome = null, ReviewReference? Reference = null);

public sealed record CaseReviewState(AssessmentRecord Assessment, long AssessmentCaseRevision, CaseProcessingInstance Process, AssessmentAuditTrail Audit);

public interface ICaseReviewAuthorizer
{
    /// <summary>Explicit current case/disposition/policy permission check. Fail closed on errors.</summary>
    bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state, CaseReviewCommand command, CaseReviewPolicy policy);
}

public interface ICaseReviewTransactionStore
{
    /// <summary>
    /// Lock the authoritative case aggregate, load its assessment/process/audit, invoke update once,
    /// and atomically commit the returned process and audit. Preserve the original assessment and audit
    /// prefix. Exceptions/cancellation roll back all writes. Serialize competing updates to the same case.
    /// Never silently retry the command against newer revisions. Missing cases must fail, never initialize.
    /// </summary>
    Task<CaseReviewState> ExecuteAsync(CaseId caseId, Func<CaseReviewState, CaseReviewState> update,
        CancellationToken cancellationToken = default);
}

public sealed class CaseReviewDeniedException : InvalidOperationException
{
    public CaseReviewDeniedException() : base("Case review is not authorized.") { }
}
public sealed class CaseReviewConflictException : InvalidOperationException
{
    public CaseReviewConflictException() : base("Case review revision or identity has already been used.") { }
}
public sealed class CaseReviewBindingException : InvalidOperationException
{
    public CaseReviewBindingException() : base("Case review identities do not match the recorded aggregate.") { }
}
public sealed class CaseReviewPolicyException : InvalidOperationException
{
    public CaseReviewPolicyException() : base("Case review policy cannot apply the configured transition.") { }
}
