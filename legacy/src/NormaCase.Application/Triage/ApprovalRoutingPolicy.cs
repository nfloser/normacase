using NormaCase.Domain.Decision;

namespace NormaCase.Application.Triage;

/// <summary>Explicit routing policy, never authorization to approve a case.</summary>
public sealed class ApprovalRoutingPolicy
{
    private readonly HashSet<AssessmentOutcome> allowed;

    public ApprovalRoutingPolicy(string id, int version, IEnumerable<AssessmentOutcome> allowedOutcomes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentNullException.ThrowIfNull(allowedOutcomes);
        allowed = allowedOutcomes.ToHashSet();
        if (allowed.Any(outcome => outcome is not (AssessmentOutcome.Supported
            or AssessmentOutcome.NotSupported or AssessmentOutcome.NotApplicable)))
            throw new ArgumentException("Only known outcomes may await approval.", nameof(allowedOutcomes));
        Id = id;
        Version = version;
    }

    public string Id { get; }
    public int Version { get; }
    public bool Allows(AssessmentOutcome outcome) => allowed.Contains(outcome);
}
