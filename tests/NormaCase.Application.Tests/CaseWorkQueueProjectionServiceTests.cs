using NormaCase.Application.Assessments;
using NormaCase.Application.Triage;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class CaseWorkQueueProjectionServiceTests
{
    private static readonly ApprovalRoutingPolicy RoutingPolicy = new(
        "synthetic-routing",
        1,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);

    [Theory]
    [InlineData("prepare-approval", "approval")]
    [InlineData("request-information", "clarification")]
    [InlineData("request-review", "review")]
    public void Process_state_selects_the_configured_queue(
        string transitionId,
        string expectedQueue)
    {
        var record = Record(TruthValue.Yes);
        var routing = new AssessmentTriageService().Route(record, RoutingPolicy);
        var definition = Process();
        var process = CaseProcessingInstance
            .Start(record.CaseId, 3, definition)
            .Apply(definition, 0, transitionId);

        var projection = new CaseWorkQueueProjectionService().Project(
            record,
            routing,
            process,
            Configuration(definition));

        Assert.Equal(CaseWorkQueueProjectionStatus.Assigned, projection.Status);
        Assert.Equal(expectedQueue, projection.QueueId);
        Assert.Equal(process.StateId, projection.StateId);
        Assert.Equal(process.Revision, projection.ProcessRevision);
        Assert.Equal(record.Result.Outcome, projection.AssessmentOutcome);
        Assert.Equal(routing.Disposition, projection.RoutingDisposition);
    }

    [Fact]
    public void Queue_membership_changes_only_when_recorded_process_state_changes()
    {
        var record = Record(TruthValue.Yes);
        var routing = new AssessmentTriageService().Route(record, RoutingPolicy);
        var definition = Process();
        var service = new CaseWorkQueueProjectionService();
        var configuration = Configuration(definition);

        var initial = CaseProcessingInstance.Start(record.CaseId, 4, definition);
        var unassigned = service.Project(record, routing, initial, configuration);
        Assert.Equal(CaseWorkQueueProjectionStatus.Unassigned, unassigned.Status);
        Assert.Null(unassigned.QueueId);

        var approval = initial.Apply(definition, 0, "prepare-approval");
        var approvalProjection = service.Project(record, routing, approval, configuration);
        Assert.Equal("approval", approvalProjection.QueueId);

        var review = approval.Apply(definition, 1, "escalate-review");
        var reviewProjection = service.Project(record, routing, review, configuration);
        Assert.Equal("review", reviewProjection.QueueId);
        Assert.Equal(2, reviewProjection.ProcessRevision);
    }

    [Fact]
    public void Assessment_or_triage_semantics_do_not_recalculate_queue_membership()
    {
        var record = Record(TruthValue.Yes, EvidenceStatus.Missing);
        var routing = new AssessmentTriageService().Route(record, RoutingPolicy);
        Assert.Equal(AssessmentRoutingDisposition.HumanReview, routing.Disposition);

        var definition = Process();
        var process = CaseProcessingInstance
            .Start(record.CaseId, 2, definition)
            .Apply(definition, 0, "prepare-approval");

        var projection = new CaseWorkQueueProjectionService().Project(
            record,
            routing,
            process,
            Configuration(definition));

        Assert.Equal("approval", projection.QueueId);
        Assert.Equal(AssessmentRoutingDisposition.HumanReview, projection.RoutingDisposition);
    }

    [Fact]
    public void Mixed_case_or_assessment_identity_fails_closed()
    {
        var record = Record(TruthValue.Yes, caseId: "case-a", assessmentId: "assessment-a");
        var routing = new AssessmentTriageService().Route(record, RoutingPolicy);
        var definition = Process();
        var service = new CaseWorkQueueProjectionService();

        var wrongCase = CaseProcessingInstance.Start(new("case-b"), 1, definition);
        Assert.Throws<CaseWorkQueueIdentityMismatchException>(
            () => service.Project(record, routing, wrongCase, Configuration(definition)));

        var otherRecord = Record(TruthValue.Yes, caseId: "case-a", assessmentId: "assessment-b");
        var otherRouting = new AssessmentTriageService().Route(otherRecord, RoutingPolicy);
        var process = CaseProcessingInstance.Start(record.CaseId, 1, definition);
        Assert.Throws<CaseWorkQueueIdentityMismatchException>(
            () => service.Project(record, otherRouting, process, Configuration(definition)));
    }

    [Fact]
    public void Ambiguous_state_assignment_is_rejected_by_configuration()
    {
        var definition = Process();

        Assert.Throws<ArgumentException>(
            () => new CaseWorkQueueConfiguration(
                "ambiguous",
                1,
                [
                    new("one", definition.Id, definition.Version, ["awaiting-approval"]),
                    new("two", definition.Id, definition.Version, ["awaiting-approval"])
                ]));
    }

    [Fact]
    public void Configuration_is_versioned_detached_and_repeatable()
    {
        var states = new List<string> { "awaiting-approval" };
        var queues = new List<CaseWorkQueueDefinition>
        {
            new("approval", Process().Id, Process().Version, states)
        };
        var configuration = new CaseWorkQueueConfiguration("queues", 7, queues);

        states.Clear();
        queues.Clear();

        Assert.Equal(7, configuration.Version);
        Assert.Single(configuration.Queues);
        Assert.Equal("awaiting-approval", configuration.Queues[0].StateIds.Single());

        var record = Record(TruthValue.No);
        var routing = new AssessmentTriageService().Route(record, RoutingPolicy);
        var definition = Process();
        var process = CaseProcessingInstance
            .Start(record.CaseId, 5, definition)
            .Apply(definition, 0, "prepare-approval");
        var service = new CaseWorkQueueProjectionService();

        var first = service.Project(record, routing, process, Configuration(definition));
        var second = service.Project(record, routing, process, Configuration(definition));

        Assert.Equal(first.QueueId, second.QueueId);
        Assert.Equal(first.ProcessRevision, second.ProcessRevision);
        Assert.Equal("awaiting-approval", process.StateId);
        Assert.Equal(1, process.Revision);
    }

    [Fact]
    public void Queue_definition_validation_is_explicit()
    {
        Assert.Throws<ArgumentException>(
            () => new CaseWorkQueueDefinition("", "workflow", 1, ["state"]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CaseWorkQueueDefinition("queue", "workflow", 0, ["state"]));
        Assert.Throws<ArgumentException>(
            () => new CaseWorkQueueDefinition("queue", "workflow", 1, []));
        Assert.Throws<ArgumentException>(
            () => new CaseWorkQueueDefinition("queue", "workflow", 1, ["state", "state"]));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CaseWorkQueueConfiguration(
                "configuration",
                0,
                [new("queue", "workflow", 1, ["state"])]));
    }

    private static AssessmentRecord Record(
        TruthValue value,
        EvidenceStatus evidence = EvidenceStatus.Present,
        string caseId = "case-queue",
        string assessmentId = "assessment-queue")
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));

        return new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = value,
                ["measurement"] = 15m,
                ["alternative_confirmed"] = TruthValue.No
            },
            new DateOnly(2026, 10, 3),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = evidence
            },
            new(
                new(assessmentId),
                new(caseId),
                "test-platform",
                new DateTimeOffset(
                    2026, 10, 3, 13, 0, 0, TimeSpan.Zero)));
    }

    private static WorkflowDefinition Process()
        => new(
            "synthetic-work-queue-process",
            2,
            "received",
            [
                new("received", false),
                new("awaiting-approval", false),
                new("waiting-information", false),
                new("manual-review", false)
            ],
            [
                new("prepare-approval", "received", "awaiting-approval"),
                new("request-information", "received", "waiting-information"),
                new("request-review", "received", "manual-review"),
                new("escalate-review", "awaiting-approval", "manual-review")
            ]);

    private static CaseWorkQueueConfiguration Configuration(
        WorkflowDefinition definition)
        => new(
            "synthetic-queues",
            3,
            [
                new("approval", definition.Id, definition.Version, ["awaiting-approval"]),
                new("clarification", definition.Id, definition.Version, ["waiting-information"]),
                new("review", definition.Id, definition.Version, ["manual-review"])
            ]);
}
