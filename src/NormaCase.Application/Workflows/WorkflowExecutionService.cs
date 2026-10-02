using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Validation;
using NormaCase.Knowledge.Workflow;

namespace NormaCase.Application.Workflows;

public sealed class WorkflowExecutionService
{
    private readonly KnowledgePackValidator _validator = new();

    public WorkflowExecution Start(
        KnowledgePack pack,
        string workflowId,
        int workflowVersion)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        if (workflowVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(workflowVersion));

        _validator.ValidateOrThrow(pack);

        var workflow = pack.Workflows.SingleOrDefault(
            candidate => string.Equals(
                    candidate.Id,
                    workflowId,
                    StringComparison.Ordinal)
                && candidate.Version == workflowVersion);

        if (workflow is null)
        {
            throw new ArgumentException(
                "The requested workflow id/version is not declared by this Knowledge Release.",
                nameof(workflowId));
        }

        var source = pack.Sources.Single(
            candidate => string.Equals(
                candidate.Id,
                workflow.SourceId,
                StringComparison.Ordinal));

        var definition =
            KnowledgeWorkflowMaterializer.Materialize(workflow);
        var instance =
            NormaCase.Domain.Workflow.WorkflowInstance.Start(definition);

        return new(
            pack.Manifest.PackId,
            pack.Manifest.ReleaseId,
            WorkflowSourceSnapshot.Copy(source),
            definition,
            instance);
    }

    public WorkflowExecution Apply(
        WorkflowExecution execution,
        string transitionId)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);

        var next = execution.Instance.Apply(
            execution.Definition,
            transitionId);

        return new(
            execution.KnowledgePackId,
            execution.KnowledgeRelease,
            execution.Source,
            execution.Definition,
            next);
    }
}
