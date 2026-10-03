using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;

namespace NormaCase.Application.Reviews;

/// <summary>Trusted, immutable entitlement snapshot; never supplied by a review request.</summary>
public sealed class CaseReviewGrant
{
    private readonly AuthenticatedReviewActor actor;
    private readonly CaseId caseId;
    private readonly CaseReviewPolicy policy;
    private readonly HashSet<HumanReviewDisposition> dispositions;

    public CaseReviewGrant(AuthenticatedReviewActor actor, CaseId caseId,
        CaseReviewPolicy policy, IEnumerable<HumanReviewDisposition> dispositions)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(dispositions);
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case entitlement required.", nameof(caseId));
        this.actor = actor;
        this.caseId = caseId;
        this.policy = policy;
        this.dispositions = new HashSet<HumanReviewDisposition>();
        foreach (var disposition in dispositions)
        {
            if (!Enum.IsDefined(disposition))
                throw new ArgumentException("Unknown review disposition.", nameof(dispositions));
            this.dispositions.Add(disposition);
        }
    }

    internal bool Allows(AuthenticatedReviewActor candidate, CaseReviewState state,
        CaseReviewCommand command, CaseReviewPolicy requestedPolicy)
        => actor == candidate
            && caseId == command.CaseId && caseId == state.Process.CaseId
            && caseId == state.Assessment.CaseId
            && policy.Id == requestedPolicy.Id && policy.Version == requestedPolicy.Version
            && policy.WorkflowId == requestedPolicy.WorkflowId
            && policy.WorkflowVersion == requestedPolicy.WorkflowVersion
            && policy.WorkflowId == state.Process.WorkflowId
            && policy.WorkflowVersion == state.Process.WorkflowVersion
            && requestedPolicy.TryGetTransition(command.Disposition, out _)
            && dispositions.Contains(command.Disposition);
}

/// <summary>Deny by default, exact identity/case/policy/action matching without wildcard grants.</summary>
public sealed class GrantedCaseReviewAuthorizer : ICaseReviewAuthorizer
{
    private readonly CaseReviewGrant[] grants;

    public GrantedCaseReviewAuthorizer(IEnumerable<CaseReviewGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        this.grants = grants.ToArray();
        if (this.grants.Any(grant => grant is null))
            throw new ArgumentException("Null review grant.", nameof(grants));
    }

    public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state,
        CaseReviewCommand command, CaseReviewPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(policy);
        return grants.Any(grant => grant.Allows(actor, state, command, policy));
    }
}
