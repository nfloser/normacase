using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Workflows;

public sealed class WorkflowRunService
{
    private readonly WorkflowExecutionService _executions = new();

    public WorkflowRunRecord Start(
        WorkflowExecution execution,
        WorkflowRunId runId,
        CaseId caseId,
        string platformVersion,
        string actorId,
        DateTimeOffset recordedAtUtc,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(execution);
        return new(runId, caseId, platformVersion,
            [new WorkflowRunEvent(null, actorId, recordedAtUtc, reason, _executions.Capture(execution))]);
    }

    public WorkflowRunRecord Apply(
        WorkflowRunRecord run,
        long expectedRevision,
        string transitionId,
        string actorId,
        DateTimeOffset recordedAtUtc,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (expectedRevision != run.Current.Revision)
            throw new WorkflowRunConcurrencyException(run.RunId);
        if (recordedAtUtc < run.History[^1].RecordedAtUtc)
            throw new ArgumentException("Workflow event time must not move backwards.", nameof(recordedAtUtc));

        var next = _executions.Apply(_executions.Restore(run.Current), transitionId);
        var item = new WorkflowRunEvent(transitionId, actorId, recordedAtUtc, reason, _executions.Capture(next));
        return new(run.RunId, run.CaseId, run.PlatformVersion, run.History.Append(item));
    }
}

public sealed class WorkflowRunConcurrencyException : InvalidOperationException
{
    public WorkflowRunConcurrencyException(WorkflowRunId runId)
        : base("Workflow run revision does not match the expected revision.") => RunId = runId;

    public WorkflowRunId RunId { get; }
}
