using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Workflows;

public sealed record WorkflowExecution
{
    internal WorkflowExecution(
        string knowledgePackId,
        string knowledgeRelease,
        WorkflowSourceSnapshot source,
        WorkflowDefinition definition,
        WorkflowInstance instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRelease);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        if (!string.Equals(
                definition.Id,
                instance.WorkflowId,
                StringComparison.Ordinal)
            || definition.Version != instance.WorkflowVersion)
        {
            throw new ArgumentException(
                "Workflow definition and instance identities must match.",
                nameof(instance));
        }

        KnowledgePackId = knowledgePackId;
        KnowledgeRelease = knowledgeRelease;
        Source = source;
        Definition = definition;
        Instance = instance;
    }

    public string KnowledgePackId { get; }

    public string KnowledgeRelease { get; }

    public WorkflowSourceSnapshot Source { get; }

    public WorkflowDefinition Definition { get; }

    public WorkflowInstance Instance { get; }
}
