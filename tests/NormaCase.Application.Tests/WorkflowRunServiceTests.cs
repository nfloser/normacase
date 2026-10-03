using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class WorkflowRunServiceTests
{
    private readonly WorkflowRunService _service = new();
    private static readonly DateTimeOffset Time = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_binds_case_and_retains_detached_historical_revisions()
    {
        var initial = Start();
        var next = _service.Apply(initial, 0, "review", "reviewer", Time.AddMinutes(1), "Unterlagen prüfen");
        var complete = _service.Apply(next, 1, "finish", "reviewer", Time.AddMinutes(2), "Prüfung abgeschlossen");

        Assert.Equal(new CaseId("case-synthetic"), complete.CaseId);
        Assert.Equal(new WorkflowRunId("run-synthetic"), complete.RunId);
        Assert.Single(initial.History);
        Assert.Equal("submitted", initial.Current.StateId);
        Assert.Equal(0, initial.Current.Revision);
        Assert.Equal(2, next.History.Count);
        Assert.Equal("reviewing", next.Current.StateId);
        Assert.Equal(3, complete.History.Count);
        Assert.Equal("done", complete.Current.StateId);
        Assert.Equal("Unterlagen prüfen", complete.History[1].Reason);
        Assert.Equal("platform-test", complete.PlatformVersion);
        Assert.NotSame(initial.Current, next.History[0].Snapshot);
    }

    [Fact]
    public void Stale_revision_backwards_time_and_invalid_transitions_fail_closed()
    {
        var run = Start();
        Assert.Throws<WorkflowRunConcurrencyException>(() => _service.Apply(
            run, 1, "review", "actor", Time, "reason"));
        Assert.Throws<ArgumentException>(() => _service.Apply(
            run, 0, "review", "actor", Time.AddTicks(-1), "reason"));
        Assert.Throws<WorkflowTransitionNotAllowedException>(() => _service.Apply(
            run, 0, "finish", "actor", Time, "reason"));
        Assert.Throws<ArgumentException>(() => _service.Apply(
            run, 0, "review", "actor", Time, " "));
        Assert.Throws<ArgumentException>(() => _service.Apply(
            run, 0, "review", " ", Time, "reason"));
        Assert.Throws<ArgumentException>(() => _service.Apply(
            run, 0, "review", "actor", Time.ToOffset(TimeSpan.FromHours(2)), "reason"));
    }

    [Fact]
    public void Equal_explicit_times_are_allowed_and_terminal_run_cannot_advance()
    {
        var next = _service.Apply(Start(), 0, "review", "actor", Time, "reason");
        var done = _service.Apply(next, 1, "finish", "actor", Time, "reason");
        Assert.Throws<WorkflowTransitionNotAllowedException>(() => _service.Apply(
            done, 2, "review", "actor", Time, "reason"));
    }

    [Fact]
    public void Constructor_detaches_history_and_rejects_empty_or_inconsistent_records()
    {
        var initial = Start();
        var history = initial.History.ToList();
        var copied = new WorkflowRunRecord(initial.RunId, initial.CaseId, initial.PlatformVersion, history);
        history.Clear();
        Assert.Single(copied.History);

        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(default, initial.CaseId, "platform", initial.History));
        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(initial.RunId, default, "platform", initial.History));
        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(initial.RunId, initial.CaseId, "platform", []));

        var advanced = _service.Apply(initial, 0, "review", "actor", Time, "reason");
        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(initial.RunId, initial.CaseId, "platform", [advanced.History[1]]));
        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(initial.RunId, initial.CaseId, "platform",
            [initial.History[0], new WorkflowRunEvent("finish", "actor", Time, "reason", advanced.Current)]));
    }

    [Fact]
    public void History_rejects_substituted_knowledge_source_or_graph()
    {
        var initial = Start();
        var next = _service.Apply(initial, 0, "review", "actor", Time, "reason");
        var snapshot = next.Current;
        var substituted = new WorkflowExecutionSnapshot(
            snapshot.KnowledgePackId, "different-release", snapshot.Source,
            snapshot.WorkflowId, snapshot.WorkflowVersion, snapshot.InitialStateId,
            snapshot.States, snapshot.Transitions, snapshot.StateId, snapshot.Revision);
        Assert.Throws<ArgumentException>(() => new WorkflowRunRecord(initial.RunId, initial.CaseId, "platform",
            [initial.History[0], new WorkflowRunEvent("review", "actor", Time, "reason", substituted)]));
    }

    [Fact]
    public void Starting_an_already_advanced_execution_is_rejected()
    {
        var execution = Execution();
        var advanced = new WorkflowExecutionService().Apply(execution, "review");
        Assert.Throws<ArgumentException>(() => _service.Start(
            advanced, new WorkflowRunId("run"), new CaseId("case"), "platform", "actor", Time, "reason"));
    }

    private WorkflowRunRecord Start() => _service.Start(
        Execution(), new WorkflowRunId("run-synthetic"), new CaseId("case-synthetic"),
        "platform-test", "creator", Time, "Synthetischer Vorgang gestartet");

    private static WorkflowExecution Execution() => new WorkflowExecutionService().Restore(new(
        "synthetic.pack", "synthetic.release",
        new WorkflowSourceSnapshot("source", "synthetic", "Synthetic source", "SYNTHETIC", "ACTIVE", "1", null, null, null, null, null, null),
        "synthetic.flow", 1, "submitted",
        [new("submitted", false), new("reviewing", false), new("done", true)],
        [new("review", "submitted", "reviewing"), new("finish", "reviewing", "done")],
        "submitted", 0));
}
