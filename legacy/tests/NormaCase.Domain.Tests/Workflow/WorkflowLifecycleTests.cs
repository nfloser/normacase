using NormaCase.Domain.Workflow;
using Xunit;

namespace NormaCase.Domain.Tests.Workflow;

public sealed class WorkflowLifecycleTests
{
    [Fact]
    public void Branching_workflow_advances_immutably_and_increments_revision_once()
    {
        var definition = ExampleDefinition();
        var started = WorkflowInstance.Start(definition);

        var reviewed = started.Apply(definition, "request_review");
        var completed = reviewed.Apply(definition, "approve");

        Assert.Equal("submitted", started.StateId);
        Assert.Equal(0, started.Revision);

        Assert.Equal("review", reviewed.StateId);
        Assert.Equal(1, reviewed.Revision);

        Assert.Equal("approved", completed.StateId);
        Assert.Equal(2, completed.Revision);
        Assert.Equal("synthetic.review-flow", completed.WorkflowId);
        Assert.Equal(3, completed.WorkflowVersion);
    }

    [Fact]
    public void Transition_that_is_not_available_from_current_state_fails_closed()
    {
        var definition = ExampleDefinition();
        var instance = WorkflowInstance.Start(definition);

        var exception = Assert.Throws<WorkflowTransitionNotAllowedException>(
            () => instance.Apply(definition, "approve"));

        Assert.Equal("approve", exception.TransitionId);
        Assert.Equal("submitted", instance.StateId);
        Assert.Equal(0, instance.Revision);
    }

    [Fact]
    public void Terminal_state_cannot_transition()
    {
        var definition = ExampleDefinition();
        var terminal = WorkflowInstance.Restore(
            definition,
            "approved",
            revision: 7);

        Assert.True(terminal.IsTerminal(definition));
        Assert.Throws<WorkflowTransitionNotAllowedException>(
            () => terminal.Apply(definition, "request_review"));
    }

    [Fact]
    public void Restored_instance_requires_a_declared_state_and_non_negative_revision()
    {
        var definition = ExampleDefinition();

        Assert.Throws<ArgumentException>(
            () => WorkflowInstance.Restore(definition, "missing", 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorkflowInstance.Restore(definition, "review", -1));
    }

    [Fact]
    public void Applying_a_different_workflow_definition_is_rejected()
    {
        var instance = WorkflowInstance.Start(ExampleDefinition());
        var other = new WorkflowDefinition(
            "synthetic.other-flow",
            3,
            "submitted",
            [
                new("submitted", false),
                new("review", false)
            ],
            [new("request_review", "submitted", "review")]);

        Assert.Throws<ArgumentException>(
            () => instance.Apply(other, "request_review"));
    }

    [Fact]
    public void Definition_defensively_copies_states_and_transitions()
    {
        var states = new[]
        {
            new WorkflowStateDefinition("submitted", false),
            new WorkflowStateDefinition("review", true)
        };
        var transitions = new[]
        {
            new WorkflowTransitionDefinition(
                "request_review",
                "submitted",
                "review")
        };

        var definition = new WorkflowDefinition(
            "synthetic.copy-test",
            1,
            "submitted",
            states,
            transitions);

        states[0] = new("rewritten", false);
        transitions[0] = new("rewritten", "submitted", "review");

        Assert.Equal("submitted", definition.States[0].Id);
        Assert.Equal("request_review", definition.Transitions[0].Id);
    }

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void Invalid_definition_is_rejected(
        string workflowId,
        int version,
        string initialState,
        WorkflowStateDefinition[] states,
        WorkflowTransitionDefinition[] transitions)
    {
        Assert.ThrowsAny<ArgumentException>(() => new WorkflowDefinition(
            workflowId,
            version,
            initialState,
            states,
            transitions));
    }

    public static TheoryData<
        string,
        int,
        string,
        WorkflowStateDefinition[],
        WorkflowTransitionDefinition[]> InvalidDefinitions()
        => new()
        {
            {
                "",
                1,
                "start",
                [new("start", false)],
                []
            },
            {
                "synthetic.flow",
                0,
                "start",
                [new("start", false)],
                []
            },
            {
                "synthetic.flow",
                1,
                "missing",
                [new("start", false)],
                []
            },
            {
                "synthetic.flow",
                1,
                "start",
                [new("start", false), new("start", true)],
                []
            },
            {
                "synthetic.flow",
                1,
                "start",
                [new("start", false), new("done", true)],
                [
                    new("advance", "start", "done"),
                    new("advance", "start", "done")
                ]
            },
            {
                "synthetic.flow",
                1,
                "start",
                [new("start", false)],
                [new("advance", "start", "missing")]
            },
            {
                "synthetic.flow",
                1,
                "start",
                [new("start", false), new("done", true)],
                [new("restart", "done", "start")]
            }
        };

    private static WorkflowDefinition ExampleDefinition()
        => new(
            "synthetic.review-flow",
            3,
            "submitted",
            [
                new("submitted", false),
                new("review", false),
                new("approved", true),
                new("rejected", true)
            ],
            [
                new("request_review", "submitted", "review"),
                new("approve", "review", "approved"),
                new("reject", "review", "rejected")
            ]);
}
