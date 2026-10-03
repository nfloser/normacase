using NormaCase.Application.Assessments;
using NormaCase.Application.Audit;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseReviewCommitServiceTests
{
    private static readonly DateTimeOffset AssessmentRecordedAt =
        new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Accepted_review_uses_authenticated_actor_and_commits_process_and_audit_together()
    {
        var fixture = Fixture();
        var authorizer = new TrackingAuthorizer(fixture.Store, allowed: true);
        var service = new CaseReviewCommitService(fixture.Store, authorizer);
        var actor = new AuthenticatedReviewActor("reviewer:synthetic-01");

        var result = await service.CommitAsync(
            actor,
            Command(
                fixture,
                "review-accept-001",
                HumanReviewDisposition.AcceptSystemResult),
            fixture.Policy);

        Assert.Equal("accepted", result.Process.StateId);
        Assert.Equal(1, result.Process.Revision);
        Assert.Equal(2, result.AuditTrail.Events.Count);
        var review = result.AuditTrail.Events[^1].Review!;
        Assert.Equal(actor.ActorId, review.ActorId);
        Assert.Equal(HumanReviewDisposition.AcceptSystemResult, review.Disposition);
        Assert.Null(review.OverrideOutcome);
        Assert.True(authorizer.WasCalledInsideTransaction);
        Assert.Equal(fixture.CaseId, authorizer.LastRequest!.CaseId);
        Assert.Equal(fixture.Policy.Id, authorizer.LastRequest.PolicyId);

        Assert.Equal("accepted", fixture.Store.Current.Process.StateId);
        Assert.Equal(2, fixture.Store.Current.AuditTrail.Events.Count);
        Assert.Equal(
            AssessmentOutcome.Supported,
            fixture.Store.Current.Assessment.Result.Outcome);
        Assert.Same(fixture.Assessment, fixture.Store.Current.Assessment);
    }

    [Fact]
    public async Task Override_uses_its_own_configured_transition_without_mutating_system_result()
    {
        var fixture = Fixture();
        var service = new CaseReviewCommitService(
            fixture.Store,
            new TrackingAuthorizer(fixture.Store, allowed: true));

        var result = await service.CommitAsync(
            new AuthenticatedReviewActor("reviewer:synthetic-02"),
            Command(
                fixture,
                "review-override-001",
                HumanReviewDisposition.Override,
                AssessmentOutcome.NotSupported),
            fixture.Policy);

        Assert.Equal("overridden", result.Process.StateId);
        var review = result.AuditTrail.Events[^1].Review!;
        Assert.Equal(HumanReviewDisposition.Override, review.Disposition);
        Assert.Equal(AssessmentOutcome.NotSupported, review.OverrideOutcome);
        Assert.Equal(
            AssessmentOutcome.Supported,
            fixture.Store.Current.Assessment.Result.Outcome);
    }

    [Fact]
    public async Task Authorization_is_case_scoped_inside_transaction_and_denial_rolls_back()
    {
        var fixture = Fixture();
        var authorizer = new TrackingAuthorizer(fixture.Store, allowed: false);
        var service = new CaseReviewCommitService(fixture.Store, authorizer);

        await Assert.ThrowsAsync<CaseReviewAuthorizationDeniedException>(
            () => service.CommitAsync(
                new AuthenticatedReviewActor("reviewer:denied"),
                Command(
                    fixture,
                    "review-denied-001",
                    HumanReviewDisposition.AcceptSystemResult),
                fixture.Policy));

        Assert.True(authorizer.WasCalledInsideTransaction);
        Assert.Equal(fixture.CaseId, authorizer.LastRequest!.CaseId);
        Assert.Equal("pending-review", fixture.Store.Current.Process.StateId);
        Assert.Equal(0, fixture.Store.Current.Process.Revision);
        Assert.Single(fixture.Store.Current.AuditTrail.Events);
    }

    [Fact]
    public async Task Stale_duplicate_and_mismatched_commands_fail_closed()
    {
        var fixture = Fixture();
        var service = new CaseReviewCommitService(
            fixture.Store,
            new TrackingAuthorizer(fixture.Store, allowed: true));

        await Assert.ThrowsAsync<CaseReviewConcurrencyException>(
            () => service.CommitAsync(
                new AuthenticatedReviewActor("reviewer:synthetic"),
                Command(
                    fixture,
                    "review-stale-001",
                    HumanReviewDisposition.AcceptSystemResult,
                    expectedProcessRevision: 1),
                fixture.Policy));

        await Assert.ThrowsAsync<CaseReviewBindingException>(
            () => service.CommitAsync(
                new AuthenticatedReviewActor("reviewer:synthetic"),
                Command(
                    fixture,
                    "review-mismatch-001",
                    HumanReviewDisposition.AcceptSystemResult,
                    assessmentId: new AssessmentId("another-assessment")),
                fixture.Policy));

        var accepted = Command(
            fixture,
            "review-duplicate-001",
            HumanReviewDisposition.AcceptSystemResult);
        await service.CommitAsync(
            new AuthenticatedReviewActor("reviewer:synthetic"),
            accepted,
            fixture.Policy);

        var duplicate = new CaseReviewCommand(
            accepted.ReviewId,
            accepted.CaseId,
            accepted.AssessmentId,
            accepted.ExpectedInputRevision,
            expectedProcessRevision: 1,
            expectedAuditSequence: 2,
            accepted.WorkflowId,
            accepted.WorkflowVersion,
            accepted.Disposition,
            accepted.RecordedAtUtc.AddMinutes(1),
            accepted.Reason,
            accepted.OverrideOutcome,
            accepted.Reference);

        await Assert.ThrowsAsync<CaseReviewDuplicateException>(
            () => service.CommitAsync(
                new AuthenticatedReviewActor("reviewer:synthetic"),
                duplicate,
                fixture.Policy));

        Assert.Equal(1, fixture.Store.Current.Process.Revision);
        Assert.Equal(2, fixture.Store.Current.AuditTrail.Events.Count);
    }

    [Fact]
    public async Task Competing_commands_have_one_winner_and_one_stale_loser()
    {
        var fixture = Fixture();
        var service = new CaseReviewCommitService(
            fixture.Store,
            new TrackingAuthorizer(fixture.Store, allowed: true));

        async Task<Exception?> Attempt(string id)
            => await Record.ExceptionAsync(
                () => service.CommitAsync(
                    new AuthenticatedReviewActor("reviewer:synthetic"),
                    Command(
                        fixture,
                        id,
                        HumanReviewDisposition.AcceptSystemResult),
                    fixture.Policy));

        var results = await Task.WhenAll(
            Attempt("review-race-a"),
            Attempt("review-race-b"));

        Assert.Single(results, error => error is null);
        Assert.Single(results, error => error is CaseReviewConcurrencyException);
        Assert.Equal(1, fixture.Store.Current.Process.Revision);
        Assert.Equal(2, fixture.Store.Current.AuditTrail.Events.Count);
    }

    [Fact]
    public async Task Store_failure_after_callback_rolls_back_process_and_audit()
    {
        var fixture = Fixture();
        fixture.Store.FailCommit = true;
        var service = new CaseReviewCommitService(
            fixture.Store,
            new TrackingAuthorizer(fixture.Store, allowed: true));

        await Assert.ThrowsAsync<SyntheticTransactionFailureException>(
            () => service.CommitAsync(
                new AuthenticatedReviewActor("reviewer:synthetic"),
                Command(
                    fixture,
                    "review-rollback-001",
                    HumanReviewDisposition.AcceptSystemResult),
                fixture.Policy));

        Assert.Equal("pending-review", fixture.Store.Current.Process.StateId);
        Assert.Equal(0, fixture.Store.Current.Process.Revision);
        Assert.Single(fixture.Store.Current.AuditTrail.Events);
    }

    [Fact]
    public async Task Queue_projection_follows_the_committed_process_state()
    {
        var fixture = Fixture();
        var queues = new CaseWorkQueueConfiguration(
            "review-queues",
            1,
            [
                new("review", fixture.Workflow.Id, fixture.Workflow.Version, ["pending-review"]),
                new("completed", fixture.Workflow.Id, fixture.Workflow.Version, ["accepted", "overridden"])
            ]);
        var projector = new CaseWorkQueueProjectionService();

        Assert.Equal(
            "review",
            projector.Project(fixture.Store.Current.Process, queues).QueueId);

        await new CaseReviewCommitService(
                fixture.Store,
                new TrackingAuthorizer(fixture.Store, allowed: true))
            .CommitAsync(
                new AuthenticatedReviewActor("reviewer:synthetic"),
                Command(
                    fixture,
                    "review-queue-001",
                    HumanReviewDisposition.AcceptSystemResult),
                fixture.Policy);

        Assert.Equal(
            "completed",
            projector.Project(fixture.Store.Current.Process, queues).QueueId);
    }

    private static CaseReviewCommand Command(
        ReviewFixture fixture,
        string reviewId,
        HumanReviewDisposition disposition,
        AssessmentOutcome? overrideOutcome = null,
        long expectedProcessRevision = 0,
        AssessmentId? assessmentId = null)
        => new(
            new ReviewId(reviewId),
            fixture.CaseId,
            assessmentId ?? fixture.Assessment.AssessmentId,
            expectedInputRevision: 3,
            expectedProcessRevision,
            expectedAuditSequence: 1,
            fixture.Workflow.Id,
            fixture.Workflow.Version,
            disposition,
            AssessmentRecordedAt.AddMinutes(10),
            "Synthetische Review-Begründung.",
            overrideOutcome);

    private static ReviewFixture Fixture()
    {
        var assessment = Assessment();
        var workflow = new WorkflowDefinition(
            "synthetic-review-process",
            2,
            "pending-review",
            [
                new("pending-review", false),
                new("accepted", true),
                new("overridden", true)
            ],
            [
                new("accept-result", "pending-review", "accepted"),
                new("override-result", "pending-review", "overridden")
            ]);
        var process = CaseProcessingInstance.Start(
            assessment.CaseId,
            3,
            workflow);
        var audit = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                assessment.AssessmentId,
                assessment.RecordedAtUtc,
                "system:assessment-recorder"));
        var state = new CaseReviewTransactionState(
            assessment,
            inputRevision: 3,
            process,
            audit);
        var store = new SyntheticTransactionStore(state);
        var policy = new CaseReviewTransitionPolicy(
            "synthetic-review-transitions",
            1,
            workflow,
            [
                new(
                    "pending-review",
                    HumanReviewDisposition.AcceptSystemResult,
                    "accept-result"),
                new(
                    "pending-review",
                    HumanReviewDisposition.Override,
                    "override-result")
            ]);
        return new(
            assessment.CaseId,
            assessment,
            workflow,
            store,
            policy);
    }

    private static AssessmentRecord Assessment()
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-a-pack.json"));

        return new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["criterion_a"] = TruthValue.Yes,
                ["criterion_b"] = TruthValue.Yes,
                ["criterion_c"] = TruthValue.No
            },
            new DateOnly(2026, 10, 3),
            evidence: null,
            new AssessmentExecutionContext(
                new AssessmentId("assessment-case-review"),
                new CaseId("case-review"),
                "platform-synthetic",
                AssessmentRecordedAt));
    }

    private sealed record ReviewFixture(
        CaseId CaseId,
        AssessmentRecord Assessment,
        WorkflowDefinition Workflow,
        SyntheticTransactionStore Store,
        CaseReviewTransitionPolicy Policy);

    private sealed class TrackingAuthorizer
        : ICaseReviewAuthorizer
    {
        private readonly SyntheticTransactionStore store;
        private readonly bool allowed;

        public TrackingAuthorizer(
            SyntheticTransactionStore store,
            bool allowed)
        {
            this.store = store;
            this.allowed = allowed;
        }

        public bool WasCalledInsideTransaction { get; private set; }

        public CaseReviewAuthorizationRequest? LastRequest { get; private set; }

        public ValueTask<bool> IsAuthorizedAsync(
            AuthenticatedReviewActor actor,
            CaseReviewAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.False(string.IsNullOrWhiteSpace(actor.ActorId));
            WasCalledInsideTransaction = store.InTransaction;
            LastRequest = request;
            return ValueTask.FromResult(allowed);
        }
    }

    private sealed class SyntheticTransactionStore
        : ICaseReviewTransactionStore
    {
        private readonly SemaphoreSlim gate = new(1, 1);

        public SyntheticTransactionStore(
            CaseReviewTransactionState state)
        {
            Current = state;
        }

        public CaseReviewTransactionState Current { get; private set; }

        public bool InTransaction { get; private set; }

        public bool FailCommit { get; set; }

        public async Task<CaseReviewTransactionMutation> ExecuteAsync(
            CaseId caseId,
            Func<
                CaseReviewTransactionState,
                CancellationToken,
                ValueTask<CaseReviewTransactionMutation>> operation,
            CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                InTransaction = true;
                if (Current.Process.CaseId != caseId)
                    throw new InvalidOperationException("Synthetic case not found.");

                var mutation = await operation(
                    Current,
                    cancellationToken);

                if (FailCommit)
                    throw new SyntheticTransactionFailureException();

                Current = new CaseReviewTransactionState(
                    Current.Assessment,
                    Current.InputRevision,
                    mutation.Process,
                    mutation.AuditTrail);

                return mutation;
            }
            finally
            {
                InTransaction = false;
                gate.Release();
            }
        }
    }

    private sealed class SyntheticTransactionFailureException
        : Exception;
}
