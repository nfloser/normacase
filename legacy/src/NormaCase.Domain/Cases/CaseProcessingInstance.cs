using NormaCase.Domain.Workflow;

namespace NormaCase.Domain.Cases;

/// <summary>
/// Explicit process state for one immutable case-input revision.
/// State/transition ids are opaque Workflow configuration, never assessment outcomes.
/// </summary>
public sealed class CaseProcessingInstance
{
    private readonly WorkflowInstance workflow;

    private CaseProcessingInstance(CaseId caseId, long caseRevision, WorkflowInstance workflow)
    {
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.", nameof(caseId));
        if (caseRevision < 1) throw new ArgumentOutOfRangeException(nameof(caseRevision));
        CaseId = caseId;
        CaseRevision = caseRevision;
        this.workflow = workflow;
    }

    public CaseId CaseId { get; }
    public long CaseRevision { get; }
    public string WorkflowId => workflow.WorkflowId;
    public int WorkflowVersion => workflow.WorkflowVersion;
    public string StateId => workflow.StateId;
    public long Revision => workflow.Revision;

    public static CaseProcessingInstance Start(CaseId caseId, long caseRevision, WorkflowDefinition definition)
        => new(caseId, caseRevision, WorkflowInstance.Start(definition));

    /// <summary>Restores structural state; historical integrity remains an adapter/replay responsibility.</summary>
    public static CaseProcessingInstance Restore(CaseId caseId, long caseRevision,
        WorkflowDefinition definition, string stateId, long revision)
        => new(caseId, caseRevision, WorkflowInstance.Restore(definition, stateId, revision));

    public bool IsTerminal(WorkflowDefinition definition) => workflow.IsTerminal(definition);

    public CaseProcessingInstance Apply(WorkflowDefinition definition, long expectedRevision, string transitionId)
    {
        if (expectedRevision != Revision) throw new CaseProcessingConcurrencyException(CaseId, CaseRevision);
        return new(CaseId, CaseRevision, workflow.Apply(definition, transitionId));
    }
}

public sealed class CaseProcessingConcurrencyException : InvalidOperationException
{
    public CaseProcessingConcurrencyException(CaseId caseId, long caseRevision)
        : base("Case processing revision does not match the expected revision.")
    {
        CaseId = caseId;
        CaseRevision = caseRevision;
    }
    public CaseId CaseId { get; }
    public long CaseRevision { get; }
}
