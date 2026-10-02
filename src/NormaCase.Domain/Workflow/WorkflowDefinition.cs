using System.Collections.ObjectModel;

namespace NormaCase.Domain.Workflow;

public sealed class WorkflowDefinition
{
    private readonly IReadOnlyDictionary<string, WorkflowStateDefinition>
        _statesById;
    private readonly IReadOnlyDictionary<string, WorkflowTransitionDefinition>
        _transitionsById;

    public WorkflowDefinition(
        string id,
        int version,
        string initialStateId,
        IEnumerable<WorkflowStateDefinition> states,
        IEnumerable<WorkflowTransitionDefinition> transitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialStateId);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(transitions);

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "Workflow version must be positive.");
        }

        var stateArray = states.ToArray();
        var transitionArray = transitions.ToArray();

        if (stateArray.Length == 0)
            throw new ArgumentException(
                "At least one workflow state is required.",
                nameof(states));

        if (stateArray.Any(state => state is null))
            throw new ArgumentException(
                "Workflow states cannot contain null.",
                nameof(states));

        if (transitionArray.Any(transition => transition is null))
            throw new ArgumentException(
                "Workflow transitions cannot contain null.",
                nameof(transitions));

        var statesById = new Dictionary<string, WorkflowStateDefinition>(
            StringComparer.Ordinal);
        foreach (var state in stateArray)
        {
            if (!statesById.TryAdd(state.Id, state))
            {
                throw new ArgumentException(
                    "Workflow state ids must be unique.",
                    nameof(states));
            }
        }

        if (!statesById.ContainsKey(initialStateId))
        {
            throw new ArgumentException(
                "Initial workflow state must be declared.",
                nameof(initialStateId));
        }

        var transitionsById =
            new Dictionary<string, WorkflowTransitionDefinition>(
                StringComparer.Ordinal);
        foreach (var transition in transitionArray)
        {
            if (!transitionsById.TryAdd(transition.Id, transition))
            {
                throw new ArgumentException(
                    "Workflow transition ids must be unique.",
                    nameof(transitions));
            }

            if (!statesById.TryGetValue(
                    transition.FromStateId,
                    out var fromState)
                || !statesById.ContainsKey(transition.ToStateId))
            {
                throw new ArgumentException(
                    "Workflow transitions must reference declared states.",
                    nameof(transitions));
            }

            if (fromState.IsTerminal)
            {
                throw new ArgumentException(
                    "Terminal workflow states cannot have outgoing transitions.",
                    nameof(transitions));
            }
        }

        Id = id;
        Version = version;
        InitialStateId = initialStateId;
        States = Array.AsReadOnly(stateArray);
        Transitions = Array.AsReadOnly(transitionArray);
        _statesById = new ReadOnlyDictionary<
            string,
            WorkflowStateDefinition>(statesById);
        _transitionsById = new ReadOnlyDictionary<
            string,
            WorkflowTransitionDefinition>(transitionsById);
    }

    public string Id { get; }

    public int Version { get; }

    public string InitialStateId { get; }

    public IReadOnlyList<WorkflowStateDefinition> States { get; }

    public IReadOnlyList<WorkflowTransitionDefinition> Transitions { get; }

    public bool TryGetState(
        string stateId,
        out WorkflowStateDefinition state)
        => _statesById.TryGetValue(stateId, out state!);

    public bool TryGetTransition(
        string transitionId,
        out WorkflowTransitionDefinition transition)
        => _transitionsById.TryGetValue(transitionId, out transition!);
}
