using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;

namespace NormaCase.Knowledge.Workflow;

public static class KnowledgeWorkflowMaterializer
{
    public static WorkflowDefinition Materialize(
        KnowledgeWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new WorkflowDefinition(
            definition.Id,
            definition.Version,
            definition.InitialStateId,
            definition.States.Select(
                state => new WorkflowStateDefinition(
                    state.Id,
                    state.Terminal)),
            definition.Transitions.Select(
                transition => new WorkflowTransitionDefinition(
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId)));
    }
}
