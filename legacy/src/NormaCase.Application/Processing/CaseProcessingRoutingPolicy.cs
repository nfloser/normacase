using System.Collections.ObjectModel;
using NormaCase.Application.Triage;

namespace NormaCase.Application.Processing;

/// <summary>
/// Versioned mapping from assessment-routing dispositions to opaque case-process transitions.
/// It is process configuration, not assessment logic or approval authorization.
/// </summary>
public sealed class CaseProcessingRoutingPolicy
{
    private readonly IReadOnlyDictionary<AssessmentRoutingDisposition, string> transitions;

    public CaseProcessingRoutingPolicy(
        string id,
        int version,
        string assessmentRoutingPolicyId,
        int assessmentRoutingPolicyVersion,
        string workflowId,
        int workflowVersion,
        IReadOnlyDictionary<AssessmentRoutingDisposition, string> transitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(assessmentRoutingPolicyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(transitions);

        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (assessmentRoutingPolicyVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(assessmentRoutingPolicyVersion));
        if (workflowVersion < 1) throw new ArgumentOutOfRangeException(nameof(workflowVersion));

        var copy = new Dictionary<AssessmentRoutingDisposition, string>();
        foreach (var mapping in transitions)
        {
            if (!Enum.IsDefined(mapping.Key))
                throw new ArgumentException("Routing disposition must be declared.", nameof(transitions));
            ArgumentException.ThrowIfNullOrWhiteSpace(mapping.Value);
            copy.Add(mapping.Key, mapping.Value);
        }

        Id = id;
        Version = version;
        AssessmentRoutingPolicyId = assessmentRoutingPolicyId;
        AssessmentRoutingPolicyVersion = assessmentRoutingPolicyVersion;
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        this.transitions = new ReadOnlyDictionary<AssessmentRoutingDisposition, string>(copy);
    }

    public string Id { get; }
    public int Version { get; }
    public string AssessmentRoutingPolicyId { get; }
    public int AssessmentRoutingPolicyVersion { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }

    public bool TryGetTransition(
        AssessmentRoutingDisposition disposition,
        out string transitionId)
        => transitions.TryGetValue(disposition, out transitionId!);
}
