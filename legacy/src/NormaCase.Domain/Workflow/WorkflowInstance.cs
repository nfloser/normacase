namespace NormaCase.Domain.Workflow;

public sealed record WorkflowInstance
{
    private WorkflowInstance(
        string workflowId,
        int workflowVersion,
        string stateId,
        long revision)
    {
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        StateId = stateId;
        Revision = revision;
    }

    public string WorkflowId { get; }

    public int WorkflowVersion { get; }

    public string StateId { get; }

    public long Revision { get; }

    public static WorkflowInstance Start(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new(
            definition.Id,
            definition.Version,
            definition.InitialStateId,
            revision: 0);
    }

    public static WorkflowInstance Restore(
        WorkflowDefinition definition,
        string stateId,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));

        if (!definition.TryGetState(stateId, out _))
        {
            throw new ArgumentException(
                "Workflow state must be declared by the definition.",
                nameof(stateId));
        }

        return new(
            definition.Id,
            definition.Version,
            stateId,
            revision);
    }

    public bool IsTerminal(WorkflowDefinition definition)
    {
        ValidateDefinitionIdentity(definition);

        return definition.TryGetState(StateId, out var state)
            && state.IsTerminal;
    }

    public WorkflowInstance Apply(
        WorkflowDefinition definition,
        string transitionId)
    {
        ValidateDefinitionIdentity(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);

        if (!definition.TryGetState(StateId, out var state)
            || state.IsTerminal
            || !definition.TryGetTransition(
                transitionId,
                out var transition)
            || !string.Equals(
                transition.FromStateId,
                StateId,
                StringComparison.Ordinal))
        {
            throw new WorkflowTransitionNotAllowedException(
                transitionId,
                StateId);
        }

        return new(
            WorkflowId,
            WorkflowVersion,
            transition.ToStateId,
            checked(Revision + 1));
    }

    private void ValidateDefinitionIdentity(
        WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.Equals(
                WorkflowId,
                definition.Id,
                StringComparison.Ordinal)
            || WorkflowVersion != definition.Version)
        {
            throw new ArgumentException(
                "Workflow definition identity does not match the instance.",
                nameof(definition));
        }
    }
}

public sealed class WorkflowTransitionNotAllowedException
    : InvalidOperationException
{
    public WorkflowTransitionNotAllowedException(
        string transitionId,
        string stateId)
        : base("Workflow transition is not available from the current state.")
    {
        TransitionId = transitionId;
        StateId = stateId;
    }

    public string TransitionId { get; }

    public string StateId { get; }
}
