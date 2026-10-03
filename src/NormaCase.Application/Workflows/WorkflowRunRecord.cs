using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Workflows;

public sealed class WorkflowRunRecord
{
    public WorkflowRunRecord(
        WorkflowRunId runId,
        CaseId caseId,
        string platformVersion,
        IEnumerable<WorkflowRunEvent> history)
    {
        if (runId.IsEmpty)
            throw new ArgumentException("Workflow run id must be explicit.", nameof(runId));
        if (caseId.IsEmpty)
            throw new ArgumentException("Case id must be explicit.", nameof(caseId));
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        ArgumentNullException.ThrowIfNull(history);
        var events = history.ToArray();
        if (events.Length == 0 || events.Any(item => item is null))
            throw new ArgumentException("Workflow history must contain a creation event.", nameof(history));

        var service = new WorkflowExecutionService();
        var first = events[0];
        var execution = service.Restore(first.Snapshot);
        if (first.TransitionId is not null
            || execution.Instance.Revision != 0
            || !string.Equals(execution.Instance.StateId, execution.Definition.InitialStateId, StringComparison.Ordinal))
            throw new ArgumentException("Workflow history must start at its initial state and revision zero.", nameof(history));

        for (var index = 1; index < events.Length; index++)
        {
            var item = events[index];
            if (item.TransitionId is null || item.RecordedAtUtc < events[index - 1].RecordedAtUtc)
                throw new ArgumentException("Workflow history must have explicit transitions and ordered UTC times.", nameof(history));

            try
            {
                execution = service.Apply(execution, item.TransitionId);
            }
            catch (WorkflowTransitionNotAllowedException)
            {
                throw new ArgumentException("Workflow history contains an unavailable transition.", nameof(history));
            }
            if (!SameSnapshot(service.Capture(execution), item.Snapshot))
                throw new ArgumentException("Workflow history does not match its declared transitions.", nameof(history));
        }

        RunId = runId;
        CaseId = caseId;
        PlatformVersion = platformVersion;
        // Every historical graph is detached, even from other immutable run versions.
        History = Array.AsReadOnly(events.Select(item => new WorkflowRunEvent(
            item.TransitionId, item.ActorId, item.RecordedAtUtc, item.Reason, item.Snapshot)).ToArray());
    }

    public WorkflowRunId RunId { get; }
    public CaseId CaseId { get; }
    public string PlatformVersion { get; }
    public IReadOnlyList<WorkflowRunEvent> History { get; }
    public WorkflowExecutionSnapshot Current => History[^1].Snapshot;

    private static bool SameSnapshot(WorkflowExecutionSnapshot left, WorkflowExecutionSnapshot right)
        => left.KnowledgePackId == right.KnowledgePackId
            && left.KnowledgeRelease == right.KnowledgeRelease
            && left.Source == right.Source
            && left.WorkflowId == right.WorkflowId
            && left.WorkflowVersion == right.WorkflowVersion
            && left.InitialStateId == right.InitialStateId
            && left.StateId == right.StateId
            && left.Revision == right.Revision
            && left.States.SequenceEqual(right.States)
            && left.Transitions.SequenceEqual(right.Transitions);
}
