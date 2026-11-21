using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Workflows;

public sealed class WorkflowExecutionSnapshot
{
    private readonly IReadOnlyList<WorkflowStateSnapshot> _states;
    private readonly IReadOnlyList<WorkflowTransitionSnapshot> _transitions;

    public WorkflowExecutionSnapshot(
        string knowledgePackId,
        string knowledgeRelease,
        WorkflowSourceSnapshot source,
        string workflowId,
        int workflowVersion,
        string initialStateId,
        IEnumerable<WorkflowStateSnapshot> states,
        IEnumerable<WorkflowTransitionSnapshot> transitions,
        string stateId,
        long revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRelease);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialStateId);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);

        var stateArray = states.ToArray();
        var transitionArray = transitions.ToArray();

        if (stateArray.Any(state => state is null))
        {
            throw new ArgumentException(
                "Workflow snapshot states cannot contain null.",
                nameof(states));
        }

        if (transitionArray.Any(transition => transition is null))
        {
            throw new ArgumentException(
                "Workflow snapshot transitions cannot contain null.",
                nameof(transitions));
        }

        KnowledgePackId = knowledgePackId;
        KnowledgeRelease = knowledgeRelease;
        Source = source;
        WorkflowId = workflowId;
        WorkflowVersion = workflowVersion;
        InitialStateId = initialStateId;
        _states = Array.AsReadOnly(stateArray);
        _transitions = Array.AsReadOnly(transitionArray);
        StateId = stateId;
        Revision = revision;
    }

    public string KnowledgePackId { get; }

    public string KnowledgeRelease { get; }

    public WorkflowSourceSnapshot Source { get; }

    public string WorkflowId { get; }

    public int WorkflowVersion { get; }

    public string InitialStateId { get; }

    public IReadOnlyList<WorkflowStateSnapshot> States => _states;

    public IReadOnlyList<WorkflowTransitionSnapshot> Transitions => _transitions;

    public string StateId { get; }

    public long Revision { get; }

    internal static WorkflowExecutionSnapshot Copy(
        WorkflowExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        return new(
            execution.KnowledgePackId,
            execution.KnowledgeRelease,
            execution.Source,
            execution.Definition.Id,
            execution.Definition.Version,
            execution.Definition.InitialStateId,
            execution.Definition.States.Select(
                state => new WorkflowStateSnapshot(
                    state.Id,
                    state.IsTerminal)),
            execution.Definition.Transitions.Select(
                transition => new WorkflowTransitionSnapshot(
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId)),
            execution.Instance.StateId,
            execution.Instance.Revision);
    }

    internal WorkflowDefinition RestoreDefinition()
        => new(
            WorkflowId,
            WorkflowVersion,
            InitialStateId,
            States.Select(
                state => new WorkflowStateDefinition(
                    state.Id,
                    state.IsTerminal)),
            Transitions.Select(
                transition => new WorkflowTransitionDefinition(
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId)));
}

public sealed record WorkflowStateSnapshot
{
    public WorkflowStateSnapshot(string id, bool isTerminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        IsTerminal = isTerminal;
    }

    public string Id { get; }

    public bool IsTerminal { get; }
}

public sealed record WorkflowTransitionSnapshot
{
    public WorkflowTransitionSnapshot(
        string id,
        string fromStateId,
        string toStateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromStateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStateId);

        Id = id;
        FromStateId = fromStateId;
        ToStateId = toStateId;
    }

    public string Id { get; }

    public string FromStateId { get; }

    public string ToStateId { get; }
}
