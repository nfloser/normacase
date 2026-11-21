using System.Collections.ObjectModel;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;

namespace NormaCase.Application.WorkQueues;

public sealed class CaseWorkQueueDefinition
{
    private readonly HashSet<string> stateIds;

    public CaseWorkQueueDefinition(
        string queueId,
        string workflowId,
        int workflowVersion,
        IEnumerable<string> stateIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(stateIds);
        if (workflowVersion < 1) throw new ArgumentOutOfRangeException(nameof(workflowVersion));

        var states = stateIds.ToArray();
        if (states.Length == 0)
            throw new ArgumentException("At least one process state is required.", nameof(stateIds));
        if (states.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Process state ids cannot be blank.", nameof(stateIds));

        this.stateIds = new HashSet<string>(states, StringComparer.Ordinal);
        if (this.stateIds.Count != states.Length)
            throw new ArgumentException("Process state ids must be unique within a queue.", nameof(stateIds));

        QueueId = queueId;
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        StateIds = Array.AsReadOnly(states);
    }

    public string QueueId { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public IReadOnlyList<string> StateIds { get; }

    public bool ContainsState(string stateId)
        => stateIds.Contains(stateId);
}

public sealed class CaseWorkQueueConfiguration
{
    public CaseWorkQueueConfiguration(
        string id,
        int version,
        IEnumerable<CaseWorkQueueDefinition> queues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(queues);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));

        var detached = queues.ToArray();
        if (detached.Any(queue => queue is null))
            throw new ArgumentException("Work queues cannot contain null.", nameof(queues));
        if (detached.Select(queue => queue.QueueId).Distinct(StringComparer.Ordinal).Count() != detached.Length)
            throw new ArgumentException("Work queue ids must be unique.", nameof(queues));

        var assignments = new HashSet<(string WorkflowId, int WorkflowVersion, string StateId)>();
        foreach (var queue in detached)
        {
            foreach (var stateId in queue.StateIds)
            {
                if (!assignments.Add((queue.WorkflowId, queue.WorkflowVersion, stateId)))
                    throw new ArgumentException(
                        "A process state can belong to only one work queue for a workflow version.",
                        nameof(queues));
            }
        }

        Id = id;
        Version = version;
        Queues = Array.AsReadOnly(detached);
    }

    public string Id { get; }
    public int Version { get; }
    public IReadOnlyList<CaseWorkQueueDefinition> Queues { get; }
}

public enum CaseWorkQueueProjectionStatus
{
    Assigned,
    Unassigned
}

public sealed class CaseWorkQueueMembership
{
    internal CaseWorkQueueMembership(
        CaseWorkQueueConfiguration configuration,
        CaseProcessingInstance process,
        string? queueId)
    {
        ConfigurationId = configuration.Id;
        ConfigurationVersion = configuration.Version;
        CaseId = process.CaseId;
        CaseRevision = process.CaseRevision;
        WorkflowId = process.WorkflowId;
        WorkflowVersion = process.WorkflowVersion;
        StateId = process.StateId;
        ProcessRevision = process.Revision;
        QueueId = queueId;
        Status = queueId is null
            ? CaseWorkQueueProjectionStatus.Unassigned
            : CaseWorkQueueProjectionStatus.Assigned;
    }

    public string ConfigurationId { get; }
    public int ConfigurationVersion { get; }
    public CaseId CaseId { get; }
    public long CaseRevision { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public string StateId { get; }
    public long ProcessRevision { get; }
    public string? QueueId { get; }
    public CaseWorkQueueProjectionStatus Status { get; }
}

public sealed class CaseWorkItemProjection
{
    internal CaseWorkItemProjection(
        CaseWorkQueueMembership membership,
        AssessmentId assessmentId,
        AssessmentOutcome assessmentOutcome,
        AssessmentRouting routing)
    {
        Membership = membership;
        AssessmentId = assessmentId;
        AssessmentOutcome = assessmentOutcome;
        RoutingDisposition = routing.Disposition;
        AssessmentRoutingPolicyId = routing.PolicyId;
        AssessmentRoutingPolicyVersion = routing.PolicyVersion;
    }

    public CaseWorkQueueMembership Membership { get; }
    public string ConfigurationId => Membership.ConfigurationId;
    public int ConfigurationVersion => Membership.ConfigurationVersion;
    public CaseId CaseId => Membership.CaseId;
    public long CaseRevision => Membership.CaseRevision;
    public string WorkflowId => Membership.WorkflowId;
    public int WorkflowVersion => Membership.WorkflowVersion;
    public string StateId => Membership.StateId;
    public long ProcessRevision => Membership.ProcessRevision;
    public string? QueueId => Membership.QueueId;
    public CaseWorkQueueProjectionStatus Status => Membership.Status;

    public AssessmentId AssessmentId { get; }
    public AssessmentOutcome AssessmentOutcome { get; }
    public AssessmentRoutingDisposition RoutingDisposition { get; }
    public string AssessmentRoutingPolicyId { get; }
    public int AssessmentRoutingPolicyVersion { get; }
}
