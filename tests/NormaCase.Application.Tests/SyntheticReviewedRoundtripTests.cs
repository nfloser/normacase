using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Outbound;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using NormaCase.Serialization;
using NormaCase.SyntheticIntegration.Outbound;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class SyntheticReviewedRoundtripTests
{
    private static readonly DateTimeOffset ReceivedAt =
        new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Inbound_case_roundtrips_through_review_to_idempotent_outbound_message()
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "demo-a-pack.json"));
        var intakeStore = new SingleIntakeStore();
        var intakeService = new NormalizedIntakeService(intakeStore);
        var request = new NormalizedIntakeRequest(
            new("roundtrip-case"),
            "synthetic-roundtrip-type",
            new IntakeProvenance(
                "synthetic-upstream",
                "upstream-case-42",
                "upstream-message-7",
                7,
                "synthetic-json-adapter",
                1,
                ReceivedAt),
            new DateOnly(2026, 10, 3),
            new Dictionary<string, CaseValue>
            {
                ["criterion_a"] = TruthValue.Yes,
                ["criterion_b"] = TruthValue.Yes
            },
            new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(),
            new Dictionary<string, IReadOnlyList<string>>());

        var intakeReceipt = await intakeService.AcceptAsync(request, pack);
        Assert.Equal(IntakeAcceptance.Accepted, intakeReceipt.Acceptance);

        var assessment = new AssessmentRecorder().Evaluate(
            pack,
            intakeReceipt.Record.Input.Facts,
            intakeReceipt.Record.Input.AssessmentDate,
            intakeReceipt.Record.Input.Evidence,
            new(
                new("roundtrip-assessment"),
                intakeReceipt.Record.CaseId,
                "synthetic-platform+roundtrip",
                ReceivedAt.AddMinutes(1)));
        Assert.Equal(AssessmentOutcome.Supported, assessment.Result.Outcome);

        var approvalPolicy = new ApprovalRoutingPolicy(
            "synthetic-roundtrip-approval",
            1,
            [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
        var assessmentRouting = new AssessmentTriageService().Route(
            assessment,
            approvalPolicy);
        Assert.Equal(
            AssessmentRoutingDisposition.ReadyForApproval,
            assessmentRouting.Disposition);

        var workflow = Workflow();
        var processPolicy = new CaseProcessingRoutingPolicy(
            "synthetic-roundtrip-process-routing",
            1,
            approvalPolicy.Id,
            approvalPolicy.Version,
            workflow.Id,
            workflow.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
                [AssessmentRoutingDisposition.Incomplete] = "request-information",
                [AssessmentRoutingDisposition.HumanReview] = "request-review"
            });
        var process = CaseProcessingInstance.Start(
            intakeReceipt.Record.CaseId,
            caseRevision: 1,
            workflow);
        var routed = new CaseProcessingRoutingService().Apply(
            assessmentRouting,
            process,
            workflow,
            processPolicy,
            expectedCaseRevision: 1,
            expectedProcessRevision: 0);
        Assert.Equal(CaseProcessingRoutingStatus.Applied, routed.Status);
        Assert.Equal("awaiting-approval", routed.Process.StateId);

        var queues = Queues(workflow);
        var workItem = new CaseWorkQueueProjectionService().Project(
            assessment,
            assessmentRouting,
            routed.Process,
            queues);
        Assert.Equal("approval", workItem.QueueId);
        Assert.Equal(1, workItem.ProcessRevision);

        var initialAudit = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                assessment.AssessmentId,
                assessment.RecordedAtUtc,
                "synthetic-ingest"));
        var reviewStore = new SingleReviewStore(
            new(assessment, 1, routed.Process, initialAudit));
        var reviewPolicy = new CaseReviewPolicy(
            "synthetic-roundtrip-review",
            1,
            workflow.Id,
            workflow.Version,
            new Dictionary<HumanReviewDisposition, string>
            {
                [HumanReviewDisposition.AcceptSystemResult] = "accept",
                [HumanReviewDisposition.Override] = "override"
            });
        var reviewed = await new CaseReviewService(
            reviewStore,
            new AllowSyntheticReview()).ReviewAsync(
                new(
                    "synthetic-local:reviewer",
                    "synthetic-local"),
                new(
                    intakeReceipt.Record.CaseId,
                    assessment.AssessmentId,
                    new("roundtrip-review"),
                    1,
                    1,
                    1,
                    ReceivedAt.AddMinutes(2),
                    HumanReviewDisposition.AcceptSystemResult,
                    "Synthetic roundtrip approval"),
                workflow,
                reviewPolicy);

        Assert.Equal("accepted", reviewed.Process.StateId);
        Assert.Equal(2, reviewed.Process.Revision);
        Assert.Equal(2, reviewed.Audit.Events[^1].Sequence);
        Assert.Same(assessment, reviewed.Assessment);

        var completedMembership = new CaseWorkQueueProjectionService().Project(
            reviewed.Process,
            queues);
        Assert.Equal("completed", completedMembership.QueueId);

        var outbound = new ReviewedCaseResultFactory().Create(
            intakeReceipt.Record,
            reviewed,
            workflow,
            new(1, 2, 2),
            "outbound-message-1",
            "correlation-upstream-message-7");

        Assert.Equal("synthetic-upstream", outbound.SourceSystemId);
        Assert.Equal("upstream-case-42", outbound.UpstreamCaseId);
        Assert.Equal("upstream-message-7", outbound.UpstreamMessageId);
        Assert.Equal(7, outbound.UpstreamRevision);
        Assert.Equal("correlation-upstream-message-7", outbound.CorrelationId);
        Assert.Equal(AssessmentOutcome.Supported, outbound.OriginalOutcome);
        Assert.Equal(AssessmentOutcome.Supported, outbound.HumanOutcome);

        var sink = new InMemoryReviewedCaseResultSink("synthetic-roundtrip-sink");
        var deliveryService = new ReviewedCaseDeliveryService(
            new InMemoryOutboundDeliveryReceiptStore());
        var deliveryRequest = new OutboundDeliveryRequest(
            "delivery-roundtrip-1",
            sink.DestinationId,
            outbound);

        var delivered = await deliveryService.DeliverAsync(deliveryRequest, sink);
        var replayedDelivery = await deliveryService.DeliverAsync(deliveryRequest, sink);

        Assert.Equal(OutboundDeliveryStatus.Delivered, delivered.Status);
        Assert.True(delivered.IsCommitted);
        Assert.Same(delivered, replayedDelivery);
        Assert.Single(sink.Deliveries);
        Assert.Equal(outbound, sink.Deliveries[0].Result);

        var outboundJson = ReviewedCaseResultJson.Serialize(outbound);
        var replayedOutbound = ReviewedCaseResultJson.Deserialize(outboundJson);
        Assert.Equal(outbound, replayedOutbound);
        Assert.Equal(outboundJson, ReviewedCaseResultJson.Serialize(replayedOutbound));
    }

    private static WorkflowDefinition Workflow()
        => new(
            "synthetic-roundtrip-workflow",
            1,
            "received",
            [
                new("received", false),
                new("awaiting-approval", false),
                new("waiting-information", false),
                new("manual-review", false),
                new("accepted", true),
                new("overridden", true)
            ],
            [
                new("prepare-approval", "received", "awaiting-approval"),
                new("request-information", "received", "waiting-information"),
                new("request-review", "received", "manual-review"),
                new("accept", "awaiting-approval", "accepted"),
                new("override", "awaiting-approval", "overridden")
            ]);

    private static CaseWorkQueueConfiguration Queues(WorkflowDefinition workflow)
        => new(
            "synthetic-roundtrip-queues",
            1,
            [
                new("approval", workflow.Id, workflow.Version, ["awaiting-approval"]),
                new("clarification", workflow.Id, workflow.Version, ["waiting-information"]),
                new("review", workflow.Id, workflow.Version, ["manual-review"]),
                new("completed", workflow.Id, workflow.Version, ["accepted", "overridden"])
            ]);

    private sealed class AllowSyntheticReview : ICaseReviewAuthorizer
    {
        public bool Authorize(
            AuthenticatedReviewActor actor,
            CaseReviewState state,
            CaseReviewCommand command,
            CaseReviewPolicy policy)
            => actor.AuthenticationAuthority == "synthetic-local"
                && command.CaseId == state.Process.CaseId
                && policy.Id == "synthetic-roundtrip-review";
    }

    private sealed class SingleReviewStore : ICaseReviewTransactionStore
    {
        private readonly object gate = new();
        private CaseReviewState state;

        public SingleReviewStore(CaseReviewState initial)
        {
            state = initial;
        }

        public Task<CaseReviewState> ExecuteAsync(
            CaseId caseId,
            Func<CaseReviewState, CaseReviewState> update,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (state.Process.CaseId != caseId)
                    throw new KeyNotFoundException();
                var next = update(state);
                cancellationToken.ThrowIfCancellationRequested();
                state = next;
                return Task.FromResult(state);
            }
        }
    }

    private sealed class SingleIntakeStore : INormalizedIntakeStore
    {
        private readonly object gate = new();
        private NormalizedIntakeRecord? recorded;

        public Task<IntakeReceipt> AppendAsync(
            NormalizedIntakeRecord record,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (recorded is null)
                {
                    recorded = record;
                    return Task.FromResult(
                        new IntakeReceipt(IntakeAcceptance.Accepted, record));
                }

                if (!recorded.HasSameContent(record))
                    throw new IntakeConflictException();

                return Task.FromResult(
                    new IntakeReceipt(IntakeAcceptance.Duplicate, recorded));
            }
        }
    }
}
