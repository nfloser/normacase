namespace NormaCase.Domain.Workflow;

public sealed record WorkflowStateDefinition
{
    public WorkflowStateDefinition(string id, bool isTerminal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        IsTerminal = isTerminal;
    }

    public string Id { get; }

    public bool IsTerminal { get; }
}
