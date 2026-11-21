using System.Collections.ObjectModel;
using NormaCase.Domain.Audit;

namespace NormaCase.Application.Reviews;

/// <summary>Opaque process configuration; neither medical policy nor authorization.</summary>
public sealed class CaseReviewPolicy
{
    private readonly IReadOnlyDictionary<HumanReviewDisposition, string> transitions;
    public CaseReviewPolicy(string id, int version, string workflowId, int workflowVersion,
        IReadOnlyDictionary<HumanReviewDisposition, string> transitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(transitions);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (workflowVersion < 1) throw new ArgumentOutOfRangeException(nameof(workflowVersion));
        var detached = new Dictionary<HumanReviewDisposition, string>();
        foreach (var entry in transitions)
        {
            if (!Enum.IsDefined(entry.Key)) throw new ArgumentException("Unknown review disposition.", nameof(transitions));
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Value);
            detached.Add(entry.Key, entry.Value);
        }
        Id = id;
        Version = version;
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        this.transitions = new ReadOnlyDictionary<HumanReviewDisposition, string>(detached);
    }
    public string Id { get; }
    public int Version { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public bool TryGetTransition(HumanReviewDisposition disposition, out string transitionId)
        => transitions.TryGetValue(disposition, out transitionId!);
}
