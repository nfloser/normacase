using NormaCase.Application.Assessments;
using NormaCase.Application.Corrections;
using NormaCase.Application.Intake;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseCorrectionServiceTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly AuthenticatedReviewActor Actor = new("synthetic-assessor", "synthetic-local");
    private static readonly WorkflowDefinition Workflow = new("correction-workflow", 1, "received",
        [new("received", false), new("waiting", false), new("manual", false), new("pending", false)],
        [new("information", "received", "waiting"), new("review", "received", "manual"), new("ready", "received", "pending")]);
    private static readonly ApprovalRoutingPolicy Triage = new("correction-triage", 1,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
    private static readonly CaseProcessingRoutingPolicy Routing = new("correction-routing", 1,
        Triage.Id, Triage.Version, Workflow.Id, Workflow.Version,
        new Dictionary<AssessmentRoutingDisposition, string> {
            [AssessmentRoutingDisposition.ReadyForApproval] = "ready",
            [AssessmentRoutingDisposition.Incomplete] = "information",
            [AssessmentRoutingDisposition.HumanReview] = "review"
        });

    [Theory]
    [InlineData("waiting", "complete-information")]
    [InlineData("manual", "correct-manual-review")]
    public void Two_explicit_policies_reassess_new_input_and_preserve_original(string stateId, string policyId)
    {
        var fixture = Fixture(stateId);
        var policy = new CaseCorrectionPolicy(policyId, 1, Workflow.Id, 1, [stateId]);
        var authorization = new Authorizer(true);
        var result = new CaseCorrectionService(authorization).Prepare(Actor, Command(fixture), fixture.Current,
            fixture.Original, fixture.Pack, Workflow, policy, Triage, Routing);
        Assert.Equal(2, result.Next.Process.CaseRevision);
        Assert.Equal("pending", result.Next.Process.StateId);
        Assert.Equal(1, result.Next.Process.Revision);
        Assert.Single(result.Next.Audit.Events);
        Assert.NotEqual(fixture.Current.Assessment.AssessmentId, result.Next.Assessment.AssessmentId);
        Assert.Equal(AssessmentOutcome.Supported, result.Next.Assessment.Result.Outcome);
        Assert.Equal(TruthValue.Unknown, fixture.Current.Assessment.Input.Facts["criterion_a"].Truth);
        Assert.Equal(1, fixture.Original.Provenance.UpstreamRevision);
        Assert.Equal(fixture.Current.Assessment.AssessmentId, result.Link.PreviousAssessmentId);
        Assert.Equal(fixture.Original.Provenance.MessageId, result.Link.PreviousMessageId);
        Assert.Equal(Actor.ActorId, result.Link.ActorId);
        Assert.Equal(policyId, result.Link.PolicyId);
        Assert.Equal("Synthetic corrected information", result.Link.Reason);
        Assert.Equal(Actor, authorization.Actor);
        Assert.Equal(policy, authorization.Policy);
    }

    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(1, 2, 1)]
    [InlineData(1, 1, 2)]
    public void Every_stale_revision_fails(long caseRevision, long processRevision, long auditRevision)
    {
        var fixture = Fixture("waiting");
        Assert.Throws<CaseCorrectionConflictException>(() => Prepare(fixture, Command(fixture) with {
            ExpectedCaseRevision = caseRevision, ExpectedProcessRevision = processRevision, ExpectedAuditRevision = auditRevision }));
    }

    [Fact]
    public void Denial_and_unconfigured_state_fail_closed()
    {
        var fixture = Fixture("waiting");
        Assert.Throws<CaseCorrectionDeniedException>(() => new CaseCorrectionService(new Authorizer(false)).Prepare(
            Actor, Command(fixture), fixture.Current, fixture.Original, fixture.Pack, Workflow, Policy(), Triage, Routing));
        var policy = new CaseCorrectionPolicy("manual-only", 1, Workflow.Id, 1, ["manual"]);
        Assert.Throws<CaseCorrectionPolicyException>(() => Prepare(fixture, Command(fixture), policy));
    }

    [Fact]
    public void Original_receipt_input_and_stream_must_match_the_assessment()
    {
        var fixture = Fixture("waiting");
        var request = Command(fixture).CorrectedInput;
        var wrong = new NormalizedIntakeRequest(request.CaseId, request.CaseTypeId,
            new("other-source", "order-one", "message-two", 2, "synthetic-json", 1, Time.AddMinutes(1)),
            request.AssessmentDate, request.Facts, request.Evidence, request.EvidenceReferences);
        Assert.Throws<CaseCorrectionBindingException>(() => Prepare(fixture, Command(fixture) with { CorrectedInput = wrong }));
        var different = fixture.Original.Input.Facts.ToDictionary(x => x.Key, x => x.Value);
        different["criterion_a"] = TruthValue.Yes;
        var original = NormalizedIntakeRecord.Restore(fixture.Original.CaseId, fixture.Original.CaseTypeId,
            fixture.Original.Provenance, fixture.Original.KnowledgePackId, fixture.Original.KnowledgeRelease,
            new(fixture.Original.Input.AssessmentDate, different, fixture.Original.Input.Evidence), fixture.Original.EvidenceReferences);
        Assert.Throws<CaseCorrectionBindingException>(() => new CaseCorrectionService(new Authorizer(true)).Prepare(
            Actor, Command(fixture), fixture.Current, original, fixture.Pack, Workflow, Policy(), Triage, Routing));
    }

    [Fact]
    public void Reused_assessment_message_and_nonconsecutive_revision_are_rejected()
    {
        var fixture = Fixture("waiting");
        Assert.Throws<CaseCorrectionConflictException>(() => Prepare(fixture, Command(fixture) with {
            AssessmentId = fixture.Current.Assessment.AssessmentId }));
        var command = Command(fixture);
        foreach (var revision in new long[] { 1, 3 })
        {
            var request = command.CorrectedInput;
            var changed = new NormalizedIntakeRequest(request.CaseId, request.CaseTypeId,
                new("source-one", "order-one", "message-two", revision, "synthetic-json", 1, Time.AddMinutes(1)),
                request.AssessmentDate, request.Facts, request.Evidence, request.EvidenceReferences);
            Assert.Throws<CaseCorrectionConflictException>(() => Prepare(fixture, command with { CorrectedInput = changed }));
        }
    }

    [Fact]
    public void Missing_values_remain_incomplete_and_never_become_approval()
    {
        var fixture = Fixture("waiting");
        var command = Command(fixture);
        var input = command.CorrectedInput;
        var unknown = new NormalizedIntakeRequest(input.CaseId, input.CaseTypeId, input.Provenance,
            input.AssessmentDate, new Dictionary<string, CaseValue>(), input.Evidence, input.EvidenceReferences);
        var result = Prepare(fixture, command with { CorrectedInput = unknown });
        Assert.Equal(AssessmentOutcome.Incomplete, result.Next.Assessment.Result.Outcome);
        Assert.Equal("waiting", result.Next.Process.StateId);
        Assert.Single(result.Next.Audit.Events);
    }

    [Fact]
    public void Policy_detaches_input_and_rejects_unbounded_or_duplicate_states()
    {
        var states = new List<string> { "waiting" };
        var policy = new CaseCorrectionPolicy("completion", 1, Workflow.Id, 1, states);
        states.Clear();
        Assert.True(policy.Allows("waiting"));
        Assert.False(policy.Allows("manual"));
        Assert.Throws<ArgumentException>(() => new CaseCorrectionPolicy("completion", 1, Workflow.Id, 1, []));
        Assert.Throws<ArgumentException>(() => new CaseCorrectionPolicy("completion", 1, Workflow.Id, 1, ["waiting", "waiting"]));
        Assert.Throws<ArgumentException>(() => new CaseCorrectionPolicy("completion", 1, Workflow.Id, 1, [new string('x', 129)]));
    }

    [Fact]
    public void Cancellation_invalid_reason_and_backward_time_produce_no_plan()
    {
        var fixture = Fixture("waiting");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new CaseCorrectionService(new Authorizer(true)).Prepare(
            Actor, Command(fixture), fixture.Current, fixture.Original, fixture.Pack, Workflow, Policy(), Triage, Routing, cancellation.Token));
        Assert.Throws<ArgumentException>(() => Prepare(fixture, Command(fixture) with { Reason = "" }));
        Assert.Throws<CaseCorrectionConflictException>(() => Prepare(fixture, Command(fixture) with { RecordedAtUtc = Time.AddSeconds(-1) }));
    }

    private static CaseCorrectionPolicy Policy() => new("completion", 1, Workflow.Id, 1, ["waiting"]);
    private static CaseCorrectionPlan Prepare(Data fixture, CaseCorrectionCommand command, CaseCorrectionPolicy? policy = null)
        => new CaseCorrectionService(new Authorizer(true)).Prepare(Actor, command, fixture.Current,
            fixture.Original, fixture.Pack, Workflow, policy ?? Policy(), Triage, Routing);
    private static CaseCorrectionCommand Command(Data fixture)
        => new("correction-one", new("assessment-two"), 1, 1, 1, Time.AddMinutes(1),
            "Synthetic corrected information", new(fixture.Original.CaseId, fixture.Original.CaseTypeId,
            new("source-one", "order-one", "message-two", 2, "synthetic-json", 1, Time.AddMinutes(1)),
            fixture.Original.Input.AssessmentDate,
            new Dictionary<string, CaseValue> { ["criterion_a"] = TruthValue.Yes, ["criterion_b"] = TruthValue.Yes, ["criterion_c"] = TruthValue.No },
            new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(),
            new Dictionary<string, IReadOnlyList<string>>()));
    private static Data Fixture(string stateId)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-a-pack.json"));
        var input = new NormalizedIntakeRequest(new("correction-case"), "synthetic-case",
            new("source-one", "order-one", "message-one", 1, "synthetic-json", 1, Time),
            new DateOnly(2026, 10, 5),
            new Dictionary<string, CaseValue> { ["criterion_b"] = TruthValue.Yes, ["criterion_c"] = TruthValue.No },
            new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(),
            new Dictionary<string, IReadOnlyList<string>>());
        var original = new NormalizedIntakeService(new UnusedIntakeStore()).Normalize(input, pack);
        var record = new AssessmentRecorder().Evaluate(pack, original.Input.Facts, original.Input.AssessmentDate,
            original.Input.Evidence, new(new("assessment-one"), original.CaseId, "test-platform", Time));
        var process = CaseProcessingInstance.Start(original.CaseId, 1, Workflow).Apply(Workflow, 0,
            stateId == "waiting" ? "information" : "review");
        return new(pack, original, new(record, 1, process,
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, Time, "synthetic-intake"))));
    }
    private sealed record Data(NormaCase.Knowledge.Model.KnowledgePack Pack, NormalizedIntakeRecord Original, CaseReviewState Current);
    private sealed class Authorizer(bool allowed) : ICaseCorrectionAuthorizer
    {
        public AuthenticatedReviewActor? Actor;
        public CaseCorrectionPolicy? Policy;
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current, CaseCorrectionCommand command, CaseCorrectionPolicy policy)
        { Actor = actor; Policy = policy; return allowed; }
    }
    private sealed class UnusedIntakeStore : INormalizedIntakeStore
    {
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Normalization has no persistence side effect.");
    }
}
