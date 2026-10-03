using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Workflows;

public interface IWorkflowRunStore
{
    Task AppendAsync(WorkflowRunRecord run, CancellationToken cancellationToken = default);
    Task<WorkflowRunRecord?> LoadLatestAsync(WorkflowRunId runId, CancellationToken cancellationToken = default);
    Task<WorkflowRunRecord?> LoadRevisionAsync(WorkflowRunId runId, long revision, CancellationToken cancellationToken = default);
}

public sealed class WorkflowRunStoreConflictException : Exception
{
    public WorkflowRunStoreConflictException(WorkflowRunId runId)
        : base("Workflow history cannot be appended.") => RunId = runId;
    public WorkflowRunId RunId { get; }
}

public sealed class WorkflowRunStoreIntegrityException : Exception
{
    public WorkflowRunStoreIntegrityException(WorkflowRunId runId)
        : base("Stored workflow history failed integrity verification.") => RunId = runId;
    public WorkflowRunId RunId { get; }
}

public sealed class WorkflowRunStoreStorageException : Exception
{
    public WorkflowRunStoreStorageException() : base("Workflow storage operation failed.") { }
}
