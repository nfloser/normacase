using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Processing;

public enum CaseProcessingRoutingStatus
{
    Applied,
    Unresolved
}

public enum CaseProcessingRoutingReason
{
    None,
    CaseMismatch,
    AssessmentPolicyMismatch,
    WorkflowMismatch,
    UnmappedDisposition,
    TransitionUnavailable
}

public sealed class CaseProcessingRoutingResult
{
    internal CaseProcessingRoutingResult(
        AssessmentRouting routing,
        CaseProcessingRoutingPolicy policy,
        CaseProcessingRoutingStatus status,
        CaseProcessingRoutingReason reason,
        CaseProcessingInstance process,
        string? transitionId)
    {
        CaseId = process.CaseId;
        CaseRevision = process.CaseRevision;
        AssessmentId = routing.AssessmentId;
        AssessmentRoutingPolicyId = routing.PolicyId;
        AssessmentRoutingPolicyVersion = routing.PolicyVersion;
        RoutingDisposition = routing.Disposition;
        PolicyId = policy.Id;
        PolicyVersion = policy.Version;
        Status = status;
        Reason = reason;
        Process = process;
        TransitionId = transitionId;
    }

    public CaseId CaseId { get; }
    public long CaseRevision { get; }
    public AssessmentId AssessmentId { get; }
    public string AssessmentRoutingPolicyId { get; }
    public int AssessmentRoutingPolicyVersion { get; }
    public AssessmentRoutingDisposition RoutingDisposition { get; }
    public string PolicyId { get; }
    public int PolicyVersion { get; }
    public CaseProcessingRoutingStatus Status { get; }
    public CaseProcessingRoutingReason Reason { get; }
    public CaseProcessingInstance Process { get; }
    public string? TransitionId { get; }
}

public sealed class CaseProcessingRoutingConcurrencyException : InvalidOperationException
{
    public CaseProcessingRoutingConcurrencyException(
        CaseId caseId,
        long expectedCaseRevision,
        long actualCaseRevision,
        long expectedProcessRevision,
        long actualProcessRevision)
        : base("Case processing revision does not match the expected case/process revision.")
    {
        CaseId = caseId;
        ExpectedCaseRevision = expectedCaseRevision;
        ActualCaseRevision = actualCaseRevision;
        ExpectedProcessRevision = expectedProcessRevision;
        ActualProcessRevision = actualProcessRevision;
    }

    public CaseId CaseId { get; }
    public long ExpectedCaseRevision { get; }
    public long ActualCaseRevision { get; }
    public long ExpectedProcessRevision { get; }
    public long ActualProcessRevision { get; }
}

/// <summary>
/// Binds an existing immutable assessment-routing decision to one explicit case-process transition.
/// It never re-inspects assessment facts and never treats approval readiness as approval.
/// </summary>
public sealed class CaseProcessingRoutingService
{
    public CaseProcessingRoutingResult Apply(
        AssessmentRouting routing,
        CaseProcessingInstance process,
        WorkflowDefinition definition,
        CaseProcessingRoutingPolicy policy,
        long expectedCaseRevision,
        long expectedProcessRevision)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(policy);

        if (expectedCaseRevision < 1)
            throw new ArgumentOutOfRangeException(nameof(expectedCaseRevision));
        if (expectedProcessRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedProcessRevision));

        if (expectedCaseRevision != process.CaseRevision
            || expectedProcessRevision != process.Revision)
        {
            throw new CaseProcessingRoutingConcurrencyException(
                process.CaseId,
                expectedCaseRevision,
                process.CaseRevision,
                expectedProcessRevision,
                process.Revision);
        }

        if (routing.CaseId != process.CaseId)
            return Unresolved(
                routing,
                policy,
                process,
                CaseProcessingRoutingReason.CaseMismatch);

        if (!string.Equals(
                routing.PolicyId,
                policy.AssessmentRoutingPolicyId,
                StringComparison.Ordinal)
            || routing.PolicyVersion != policy.AssessmentRoutingPolicyVersion)
        {
            return Unresolved(
                routing,
                policy,
                process,
                CaseProcessingRoutingReason.AssessmentPolicyMismatch);
        }

        if (!string.Equals(process.WorkflowId, definition.Id, StringComparison.Ordinal)
            || process.WorkflowVersion != definition.Version
            || !string.Equals(policy.WorkflowId, definition.Id, StringComparison.Ordinal)
            || policy.WorkflowVersion != definition.Version)
        {
            return Unresolved(
                routing,
                policy,
                process,
                CaseProcessingRoutingReason.WorkflowMismatch);
        }

        if (!policy.TryGetTransition(routing.Disposition, out var transitionId))
            return Unresolved(
                routing,
                policy,
                process,
                CaseProcessingRoutingReason.UnmappedDisposition);

        if (!definition.TryGetTransition(transitionId, out var transition)
            || !string.Equals(
                transition.FromStateId,
                process.StateId,
                StringComparison.Ordinal))
        {
            return Unresolved(
                routing,
                policy,
                process,
                CaseProcessingRoutingReason.TransitionUnavailable);
        }

        var next = process.Apply(
            definition,
            expectedProcessRevision,
            transitionId);

        return new(
            routing,
            policy,
            CaseProcessingRoutingStatus.Applied,
            CaseProcessingRoutingReason.None,
            next,
            transitionId);
    }

    private static CaseProcessingRoutingResult Unresolved(
        AssessmentRouting routing,
        CaseProcessingRoutingPolicy policy,
        CaseProcessingInstance process,
        CaseProcessingRoutingReason reason)
        => new(
            routing,
            policy,
            CaseProcessingRoutingStatus.Unresolved,
            reason,
            process,
            transitionId: null);
}
