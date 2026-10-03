using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseReviewServiceTests
{
    private static readonly WorkflowDefinition Workflow = new("synthetic-review", 1, "pending",
        [new("pending", false), new("accepted", true), new("corrected", true)],
        [new("accept", "pending", "accepted"), new("correct", "pending", "corrected")]);
    private static readonly CaseReviewPolicy Policy = new("synthetic-review-policy", 1, Workflow.Id, 1,
        new Dictionary<HumanReviewDisposition, string>
        {
            [HumanReviewDisposition.AcceptSystemResult] = "accept",
            [HumanReviewDisposition.Override] = "correct"
        });
    private static readonly DateTimeOffset Time = new(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
    private static readonly AuthenticatedReviewActor Actor = new("synthetic-assessor", "synthetic-local-authority");

    [Theory]
    [InlineData(HumanReviewDisposition.AcceptSystemResult, "accepted")]
    [InlineData(HumanReviewDisposition.Override, "corrected")]
    public async Task Review_commits_process_and_audit_without_changing_assessment(HumanReviewDisposition disposition, string state)
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        var authorizer = new Authorizer(true);
        var result = await new CaseReviewService(store, authorizer).ReviewAsync(Actor, Command(disposition), Workflow, Policy);
        Assert.Equal(state, result.Process.StateId);
        Assert.Equal(1, result.Process.Revision);
        Assert.Equal(2, result.Audit.Events.Count);
        Assert.Same(original.Assessment, result.Assessment);
        Assert.Equal(AssessmentOutcome.Supported, result.Assessment.Result.Outcome);
        Assert.Single(original.Audit.Events);
        Assert.Equal("pending", original.Process.StateId);
        var review = result.Audit.Events[1].Review!;
        Assert.Equal(Actor.ActorId, review.ActorId);
        Assert.Equal(Time.AddMinutes(1), review.RecordedAt);
        Assert.Equal("Synthetic review reason", review.Reason);
        Assert.Equal(disposition, review.Disposition);
        Assert.Equal(disposition == HumanReviewDisposition.Override ? AssessmentOutcome.NotSupported : null, review.OverrideOutcome);
        Assert.Equal(Actor, authorizer.Actor);
        Assert.Equal(Policy, authorizer.Policy);
        var queues = new CaseWorkQueueConfiguration("review-queues", 1,
            [new("pending-queue", Workflow.Id, 1, ["pending"]), new("done-queue", Workflow.Id, 1, ["accepted", "corrected"])]);
        Assert.Equal("done-queue", new CaseWorkQueueProjectionService().Project(store.State.Process, queues).QueueId);
    }

    [Fact]
    public async Task Authorization_denial_never_appends_or_transitions()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        await Assert.ThrowsAsync<CaseReviewDeniedException>(() => new CaseReviewService(store, new Authorizer(false))
            .ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Same(original, store.State);
    }

    [Theory]
    [InlineData(2, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 0, 2)]
    public async Task Each_stale_revision_fails_without_writing(long caseRevision, long processRevision, long auditRevision)
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        await Assert.ThrowsAsync<CaseReviewConflictException>(() => Service(store).ReviewAsync(Actor,
            Command() with { ExpectedCaseRevision = caseRevision, ExpectedProcessRevision = processRevision, ExpectedAuditRevision = auditRevision }, Workflow, Policy));
        Assert.Same(original, store.State);
    }

    [Fact]
    public async Task Competing_reviews_have_one_winner_and_no_automatic_retry()
    {
        var store = new ReferenceStore(Initial());
        var service = Service(store);
        async Task<bool> Attempt(string id)
        {
            try { await service.ReviewAsync(Actor, Command() with { ReviewId = new(id) }, Workflow, Policy); return true; }
            catch (CaseReviewConflictException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(() => Attempt("review-a")), Task.Run(() => Attempt("review-b")));
        Assert.Single(results, success => success);
        Assert.Equal(2, store.State.Audit.Events.Count);
        Assert.Equal(1, store.State.Process.Revision);
    }

    [Fact]
    public async Task Wrong_assessment_and_invalid_audit_binding_fail_closed()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        await Assert.ThrowsAsync<CaseReviewBindingException>(() => Service(store).ReviewAsync(Actor,
            Command() with { AssessmentId = new("other-assessment") }, Workflow, Policy));
        Assert.Same(original, store.State);
        store = new ReferenceStore(original with { Audit = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(1, original.Assessment.AssessmentId, Time.AddSeconds(1), "synthetic-ingest")) });
        await Assert.ThrowsAsync<CaseReviewBindingException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Single(store.State.Audit.Events);
    }

    [Fact]
    public async Task Invalid_review_and_transaction_failure_roll_back_both_states()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(store).ReviewAsync(Actor,
            Command(HumanReviewDisposition.Override) with { OverrideOutcome = null }, Workflow, Policy));
        Assert.Same(original, store.State);
        store.FailBeforeCommit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Same(original, store.State);
    }

    [Fact]
    public async Task Reused_review_identity_and_unmapped_disposition_are_rejected()
    {
        var store = new ReferenceStore(Initial());
        var reviewed = await Service(store).ReviewAsync(Actor, Command(), Workflow, Policy);
        store = new ReferenceStore(reviewed with { Process = CaseProcessingInstance.Start(reviewed.Process.CaseId, 1, Workflow) });
        await Assert.ThrowsAsync<CaseReviewConflictException>(() => Service(store).ReviewAsync(Actor,
            Command() with { ExpectedAuditRevision = 2 }, Workflow, Policy));
        var emptyPolicy = new CaseReviewPolicy("empty", 1, Workflow.Id, 1, new Dictionary<HumanReviewDisposition, string>());
        store = new ReferenceStore(Initial());
        await Assert.ThrowsAsync<CaseReviewPolicyException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, emptyPolicy));
    }

    [Fact]
    public async Task Assessment_input_revision_must_match_process_and_case_binding()
    {
        var original = Initial();
        var store = new ReferenceStore(original with { AssessmentCaseRevision = 2 });
        await Assert.ThrowsAsync<CaseReviewBindingException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Single(store.State.Audit.Events);
        store = new ReferenceStore(original with { Process = CaseProcessingInstance.Start(new("other-case"), 1, Workflow) });
        await Assert.ThrowsAsync<CaseReviewBindingException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Single(store.State.Audit.Events);
    }

    [Fact]
    public async Task Workflow_mismatch_and_unavailable_transition_never_append()
    {
        var store = new ReferenceStore(Initial());
        var mismatch = new CaseReviewPolicy("wrong-workflow", 1, "other-workflow", 1,
            new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.AcceptSystemResult] = "accept" });
        await Assert.ThrowsAsync<CaseReviewPolicyException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, mismatch));
        var current = store.State;
        store = new ReferenceStore(current with { Process = current.Process.Apply(Workflow, 0, "accept") });
        await Assert.ThrowsAsync<CaseReviewPolicyException>(() => Service(store).ReviewAsync(Actor,
            Command() with { ExpectedProcessRevision = 1 }, Workflow, Policy));
        Assert.Single(store.State.Audit.Events);
    }

    [Fact]
    public async Task Cancellation_and_backward_timestamp_leave_transaction_unchanged()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(store).ReviewAsync(Actor, Command(), Workflow, Policy, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(store).ReviewAsync(Actor,
            Command() with { RecordedAtUtc = Time.AddSeconds(-1) }, Workflow, Policy));
        Assert.Same(original, store.State);
    }

    [Fact]
    public async Task Missing_authentication_context_fails_before_entering_store()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service(store).ReviewAsync(null!, Command(), Workflow, Policy));
        Assert.Same(original, store.State);
        Assert.Throws<ArgumentException>(() => new AuthenticatedReviewActor("", "synthetic"));
        Assert.Throws<ArgumentException>(() => new AuthenticatedReviewActor("synthetic", ""));
    }

    [Fact]
    public async Task Cancellation_during_authorization_and_authorizer_failure_roll_back()
    {
        var store = new ReferenceStore(Initial());
        var original = store.State;
        using var cancellation = new CancellationTokenSource();
        var authorizer = new CallbackAuthorizer(() => { cancellation.Cancel(); return true; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CaseReviewService(store, authorizer)
            .ReviewAsync(Actor, Command(), Workflow, Policy, cancellation.Token));
        Assert.Same(original, store.State);
        authorizer = new CallbackAuthorizer(() => throw new InvalidOperationException("Synthetic authorization failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CaseReviewService(store, authorizer)
            .ReviewAsync(Actor, Command(), Workflow, Policy));
        Assert.Same(original, store.State);
    }

    private sealed class CallbackAuthorizer(Func<bool> authorize) : ICaseReviewAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state, CaseReviewCommand command, CaseReviewPolicy policy)
            => authorize();
    }

    [Fact]
    public void Policy_detaches_mapping_and_rejects_unknown_dispositions()
    {
        var mappings = new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.Override] = "correct" };
        var policy = new CaseReviewPolicy("detached", 1, Workflow.Id, 1, mappings);
        mappings.Clear();
        Assert.True(policy.TryGetTransition(HumanReviewDisposition.Override, out var transition));
        Assert.Equal("correct", transition);
        Assert.Throws<ArgumentException>(() => new CaseReviewPolicy("invalid", 1, Workflow.Id, 1,
            new Dictionary<HumanReviewDisposition, string> { [(HumanReviewDisposition)999] = "accept" }));
    }

    private static CaseReviewService Service(ReferenceStore store) => new(store, new Authorizer(true));
    private static CaseReviewCommand Command(HumanReviewDisposition disposition = HumanReviewDisposition.AcceptSystemResult)
        => new(new("case-review"), new("assessment-review"), new("review-one"), 1, 0, 1,
            Time.AddMinutes(1), disposition, "Synthetic review reason", disposition == HumanReviewDisposition.Override ? AssessmentOutcome.NotSupported : null);
    private static CaseReviewState Initial()
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-a-pack.json"));
        var record = new AssessmentRecorder().Evaluate(pack, new Dictionary<string, CaseValue> { ["criterion_a"] = TruthValue.Yes, ["criterion_b"] = TruthValue.Yes, ["criterion_c"] = TruthValue.No },
            new DateOnly(2026, 10, 3), null, new(new("assessment-review"), new("case-review"), "test-platform", Time));
        return new(record, 1, CaseProcessingInstance.Start(record.CaseId, 1, Workflow),
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, Time, "synthetic-ingest")));
    }

    private sealed class Authorizer(bool allowed) : ICaseReviewAuthorizer
    {
        public AuthenticatedReviewActor? Actor;
        public CaseReviewPolicy? Policy;
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state, CaseReviewCommand command, CaseReviewPolicy policy)
        { Actor = actor; Policy = policy; return allowed; }
    }

    // Contract fixture only: one lock covers read/check/append/transition/commit.
    private sealed class ReferenceStore(CaseReviewState state) : ICaseReviewTransactionStore
    {
        private readonly object gate = new();
        public CaseReviewState State = state;
        public bool FailBeforeCommit;
        public Task<CaseReviewState> ExecuteAsync(CaseId caseId, Func<CaseReviewState, CaseReviewState> update, CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var next = update(State);
                if (FailBeforeCommit) throw new InvalidOperationException("Synthetic transaction failure");
                cancellationToken.ThrowIfCancellationRequested();
                State = next;
                return Task.FromResult(State);
            }
        }
    }
}
