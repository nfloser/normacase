using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using Xunit;

namespace NormaCase.Domain.Tests.Cases;

public sealed class CaseProcessingInstanceTests
{
    [Fact]
    public void Explicit_transitions_keep_case_revision_and_original_state_immutable()
    {
        var definition = Process();
        var original = CaseProcessingInstance.Start(new("synthetic-case"), 7, definition);
        var next = original.Apply(definition, 0, "route");
        Assert.Equal("received", original.StateId);
        Assert.Equal(0, original.Revision);
        Assert.Equal("prepared", next.StateId);
        Assert.Equal(1, next.Revision);
        Assert.Equal(original.CaseId, next.CaseId);
        Assert.Equal(7, next.CaseRevision);
        Assert.Equal(definition.Id, next.WorkflowId);
        Assert.Equal(definition.Version, next.WorkflowVersion);
        var final = next.Apply(definition, 1, "finish");
        Assert.True(final.IsTerminal(definition));
        Assert.Throws<WorkflowTransitionNotAllowedException>(() => final.Apply(definition, 2, "finish"));
    }

    [Fact]
    public void Stale_revision_wrong_definition_and_unavailable_transition_fail()
    {
        var original = CaseProcessingInstance.Start(new("synthetic-case"), 1, Process());
        Assert.Throws<CaseProcessingConcurrencyException>(() => original.Apply(Process(), 1, "route"));
        Assert.Throws<ArgumentException>(() => original.Apply(Process(version: 2), 0, "route"));
        Assert.Throws<WorkflowTransitionNotAllowedException>(() => original.Apply(Process(), 0, "finish"));
    }

    [Fact]
    public void Case_identity_revision_and_restore_metadata_are_explicit()
    {
        Assert.Throws<ArgumentException>(() => CaseProcessingInstance.Start(default, 1, Process()));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseProcessingInstance.Start(new("case"), 0, Process()));
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseProcessingInstance.Restore(new("case"), 1, Process(), "prepared", -1));
        Assert.Throws<ArgumentException>(() => CaseProcessingInstance.Restore(new("case"), 1, Process(), "undeclared", 2));
        var restored = CaseProcessingInstance.Restore(new("case"), 1, Process(), "prepared", 8);
        Assert.Equal(9, restored.Apply(Process(), 8, "finish").Revision);
        Assert.Throws<OverflowException>(() => CaseProcessingInstance.Restore(new("case"), 1, Process(), "prepared", long.MaxValue).Apply(Process(), long.MaxValue, "finish"));
    }

    [Fact]
    public void State_names_are_opaque_configuration_not_medical_or_assessment_enums()
    {
        var definition = new WorkflowDefinition("process.other", 3, "x",
            [new("x", false), new("awaiting-source", false), new("integration-error", false), new("z", true)],
            [new("request-info", "x", "awaiting-source"), new("technical", "x", "integration-error"), new("finish", "awaiting-source", "z")]);
        var initial = CaseProcessingInstance.Start(new("case"), 12, definition);
        Assert.Equal("awaiting-source", initial.Apply(definition, 0, "request-info").StateId);
        Assert.Equal("integration-error", initial.Apply(definition, 0, "technical").StateId);
        Assert.Equal("x", initial.StateId);
    }

    [Fact]
    public void Invalid_graphs_remain_rejected_by_the_existing_workflow_contract()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition("case-process", 1, "a",
            [new("a", false)], [new("bad", "a", "missing")]));
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition("case-process", 1, "a",
            [new("a", true), new("b", true)], [new("bad", "a", "b")]));
    }

    private static WorkflowDefinition Process(int version = 1) => new("synthetic-process", version, "received",
        [new("received", false), new("prepared", false), new("done", true)],
        [new("route", "received", "prepared"), new("finish", "prepared", "done")]);
}
