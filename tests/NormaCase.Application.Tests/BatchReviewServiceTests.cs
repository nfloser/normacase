using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class BatchReviewServiceTests
{
    private static readonly WorkflowDefinition Workflow = new("synthetic-batch-review", 1, "pending",
        [new("pending", false), new("accepted", true)], [new("accept", "pending", "accepted")]);
    private static readonly CaseReviewPolicy ReviewPolicy = new("synthetic-single-review", 2, Workflow.Id, 1,
        new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.AcceptSystemResult] = "accept" });
    private static readonly AuthenticatedReviewActor Actor = new("synthetic-local:user-alice", "synthetic-local");
    private static readonly DateTimeOffset Time = new(2026, 10, 4, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Continue_mode_commits_authorized_cases_and_returns_bounded_failures_in_order()
    {
        var store = Store("case-a", "case-b", "case-c");
        var authorizer = new SelectiveAuthorizer("case-b");
        var policy = Policy(BatchReviewFailureMode.Continue);
        var result = await Service(store, authorizer).ReviewAsync(Actor,
            [Command("case-a"), Command("case-b"), Command("case-c")], Workflow, ReviewPolicy, policy);

        Assert.Equal(policy.Id, result.PolicyId);
        Assert.Equal(policy.Version, result.PolicyVersion);
        Assert.Equal(
            [BatchReviewItemStatus.Committed, BatchReviewItemStatus.Denied, BatchReviewItemStatus.Committed],
            result.Items.Select(item => item.Status));
        Assert.Equal(new[] { "case-a", "case-b", "case-c" },
            result.Items.Select(item => item.CaseId.Value));
        Assert.Equal("accepted", store.States["case-a"].Process.StateId);
        Assert.Equal("pending", store.States["case-b"].Process.StateId);
        Assert.Equal("accepted", store.States["case-c"].Process.StateId);
        Assert.Equal(3, authorizer.Seen.Count);
    }

    [Fact]
    public async Task Stop_mode_marks_remaining_commands_unattempted_without_rolling_back_prior_commits()
    {
        var store = Store("case-a", "case-b", "case-c");
        var result = await Service(store, new SelectiveAuthorizer("case-b")).ReviewAsync(Actor,
            [Command("case-a"), Command("case-b"), Command("case-c")], Workflow, ReviewPolicy,
            Policy(BatchReviewFailureMode.Stop));

        Assert.Equal(
            [BatchReviewItemStatus.Committed, BatchReviewItemStatus.Denied, BatchReviewItemStatus.NotAttempted],
            result.Items.Select(item => item.Status));
        Assert.Equal("accepted", store.States["case-a"].Process.StateId);
        Assert.Equal("pending", store.States["case-b"].Process.StateId);
        Assert.Equal("pending", store.States["case-c"].Process.StateId);
    }

    [Fact]
    public async Task Conflicts_and_policy_rejections_are_case_local_and_never_retried()
    {
        var store = Store("case-a", "case-b", "case-c");
        var commands = new[]
        {
            Command("case-a") with { ExpectedProcessRevision = 1 },
            Command("case-b"),
            Command("case-c") with { ExpectedProcessRevision = 1 }
        };
        store.States["case-c"] = store.States["case-c"] with
        {
            Process = store.States["case-c"].Process.Apply(Workflow, 0, "accept")
        };
        var result = await Service(store, new SelectiveAuthorizer()).ReviewAsync(Actor, commands,
            Workflow, ReviewPolicy, Policy(BatchReviewFailureMode.Continue));

        Assert.Equal(
            [BatchReviewItemStatus.Conflict, BatchReviewItemStatus.Committed, BatchReviewItemStatus.PolicyRejected],
            result.Items.Select(item => item.Status));
        Assert.Equal(3, store.Attempts);
    }

    [Fact]
    public async Task Invalid_duplicate_oversized_and_mismatched_batches_fail_before_any_mutation()
    {
        var store = Store("case-a", "case-b");
        var service = Service(store, new SelectiveAuthorizer());
        var policy = Policy(BatchReviewFailureMode.Continue, maximum: 2);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(Actor,
            [Command("case-a"), Command("case-a") with { ReviewId = new("other-review") }],
            Workflow, ReviewPolicy, policy));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(Actor,
            [Command("case-a"), Command("case-b") with { ReviewId = new("review-case-a") }],
            Workflow, ReviewPolicy, policy));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ReviewAsync(Actor,
            [Command("case-a"), Command("case-b"), Command("case-c")], Workflow, ReviewPolicy, policy));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(Actor,
            [Command("case-a")], Workflow, ReviewPolicy,
            new("wrong", 1, "other-review-policy", 1, 2, BatchReviewFailureMode.Continue)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(Actor,
            [Command("case-a"), Command("case-b") with { Reason = "" }], Workflow, ReviewPolicy, policy));

        Assert.Equal(0, store.Attempts);
        Assert.All(store.States.Values, state => Assert.Equal("pending", state.Process.StateId));
    }

    [Fact]
    public async Task Cancellation_and_unexpected_store_failure_propagate_without_synthetic_result()
    {
        var store = Store("case-a", "case-b");
        var service = Service(store, new SelectiveAuthorizer());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReviewAsync(Actor,
            [Command("case-a")], Workflow, ReviewPolicy, Policy(BatchReviewFailureMode.Continue), cancellation.Token));
        Assert.Equal(0, store.Attempts);

        store.FailCase = "case-b";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReviewAsync(Actor,
            [Command("case-a"), Command("case-b")], Workflow, ReviewPolicy,
            Policy(BatchReviewFailureMode.Continue)));
        Assert.Equal("accepted", store.States["case-a"].Process.StateId);
        Assert.Equal("pending", store.States["case-b"].Process.StateId);
    }

    [Fact]
    public void Policy_is_explicit_versioned_and_hard_bounded()
    {
        var policy = Policy(BatchReviewFailureMode.Stop, 37);
        Assert.Equal(37, policy.MaximumCommands);
        Assert.Equal(BatchReviewFailureMode.Stop, policy.FailureMode);
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(BatchReviewFailureMode.Continue, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(BatchReviewFailureMode.Continue, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy((BatchReviewFailureMode)999));
    }

    private static BatchReviewPolicy Policy(BatchReviewFailureMode mode, int maximum = 10)
        => new("synthetic-batch-policy", 3, ReviewPolicy.Id, ReviewPolicy.Version, maximum, mode);

    private static BatchReviewService Service(MultiCaseStore store, ICaseReviewAuthorizer authorizer)
        => new(new CaseReviewService(store, authorizer));

    private static CaseReviewCommand Command(string caseId)
        => new(new(caseId), new("assessment-" + caseId), new("review-" + caseId), 1, 0, 1,
            Time.AddMinutes(1), HumanReviewDisposition.AcceptSystemResult, "Synthetische Sammelprüfung");

    private static MultiCaseStore Store(params string[] cases)
        => new(cases.ToDictionary(id => id, Initial, StringComparer.Ordinal));

    private static CaseReviewState Initial(string caseId)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-a-pack.json"));
        var record = new AssessmentRecorder().Evaluate(pack,
            new Dictionary<string, CaseValue>
            {
                ["criterion_a"] = TruthValue.Yes,
                ["criterion_b"] = TruthValue.Yes,
                ["criterion_c"] = TruthValue.No
            }, new DateOnly(2026, 10, 4), null,
            new(new("assessment-" + caseId), new(caseId), "test-platform", Time));
        return new(record, 1, CaseProcessingInstance.Start(record.CaseId, 1, Workflow),
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(
                1, record.AssessmentId, Time, "synthetic-ingest")));
    }

    private sealed class SelectiveAuthorizer(params string[] deniedCases) : ICaseReviewAuthorizer
    {
        private readonly HashSet<string> denied = deniedCases.ToHashSet(StringComparer.Ordinal);
        public List<string> Seen { get; } = [];
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state,
            CaseReviewCommand command, CaseReviewPolicy policy)
        {
            Seen.Add(command.CaseId.Value);
            return !denied.Contains(command.CaseId.Value);
        }
    }

    private sealed class MultiCaseStore(Dictionary<string, CaseReviewState> states) : ICaseReviewTransactionStore
    {
        public Dictionary<string, CaseReviewState> States { get; } = states;
        public int Attempts { get; private set; }
        public string? FailCase { get; set; }

        public Task<CaseReviewState> ExecuteAsync(CaseId caseId,
            Func<CaseReviewState, CaseReviewState> update,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            if (caseId.Value == FailCase) throw new InvalidOperationException("Synthetic storage failure");
            var next = update(States[caseId.Value]);
            cancellationToken.ThrowIfCancellationRequested();
            States[caseId.Value] = next;
            return Task.FromResult(next);
        }
    }
}
