namespace NormaCase.Domain.Workflow;

public sealed record WorkflowTransitionDefinition
{
    public WorkflowTransitionDefinition(
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
