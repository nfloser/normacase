using NormaCase.Application.Assessments;
using NormaCase.Application.Processing;
using NormaCase.Application.Triage;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal static class SyntheticWorkQueueEndpoints
{
    private const string DemoPackId = "synthetic.demo-c";

    internal static void Map(
        WebApplication app,
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        var workload = Build(packs, platformVersion);

        app.MapGet("/api/work-queues", () => Results.Json(new
        {
            configurationId = workload.Configuration.Id,
            configurationVersion = workload.Configuration.Version,
            queues = workload.Configuration.Queues.Select(queue => new
            {
                id = queue.QueueId,
                items = workload.Cases.Values
                    .Where(item => string.Equals(
                        item.Membership.QueueId,
                        queue.QueueId,
                        StringComparison.Ordinal))
                    .OrderBy(item => item.Membership.CaseId.Value, StringComparer.Ordinal)
                    .Select(Summary)
                    .ToArray()
            }).ToArray()
        }));

        app.MapGet("/api/work-queues/cases/{caseId}", (string caseId) =>
        {
            if (!workload.Cases.TryGetValue(caseId, out var item))
                return DemoHost.Error("unknown_work_item", 404);

            var record = item.Assessment;
            var routing = item.Routing;

            return Results.Json(new
            {
                caseId = item.Membership.CaseId.Value,
                caseRevision = item.Membership.CaseRevision,
                queueId = item.Membership.QueueId,
                workflowId = item.Membership.WorkflowId,
                workflowVersion = item.Membership.WorkflowVersion,
                stateId = item.Membership.StateId,
                processRevision = item.Membership.ProcessRevision,
                hasAssessment = record is not null,
                knowledgePackId = record?.KnowledgePackId,
                assessmentId = record?.AssessmentId.Value,
                assessmentOutcome = record is null
                    ? null
                    : Outcome(record.Result.Outcome),
                assessmentJson = record is null
                    ? null
                    : AssessmentJson.Serialize(record.Result, platformVersion),
                evidence = record?.Input.Evidence
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value.ToString().ToUpperInvariant(),
                        StringComparer.Ordinal),
                routing = routing is null
                    ? null
                    : new
                    {
                        disposition = Disposition(routing.Disposition),
                        policyId = routing.PolicyId,
                        policyVersion = routing.PolicyVersion,
                        reasons = routing.Reasons.Select(reason => new
                        {
                            code = reason.Code.ToString(),
                            reference = reason.Reference
                        }).ToArray()
                    },
                technicalReasonCode = item.TechnicalReasonCode
            });
        });
    }

    private static object Summary(SyntheticWorkQueueCase item)
    {
        var record = item.Assessment;
        return new
        {
            caseId = item.Membership.CaseId.Value,
            caseRevision = item.Membership.CaseRevision,
            stateId = item.Membership.StateId,
            processRevision = item.Membership.ProcessRevision,
            hasAssessment = record is not null,
            assessmentOutcome = record is null
                ? null
                : Outcome(record.Result.Outcome),
            routingDisposition = item.Routing is null
                ? null
                : Disposition(item.Routing.Disposition)
        };
    }

    private static SyntheticWorkload Build(
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        if (!packs.TryGetValue(DemoPackId, out var pack))
            throw new InvalidOperationException("Synthetic work-queue pack is not installed.");

        var definition = new WorkflowDefinition(
            "synthetic.work-queue-process",
            1,
            "received",
            [
                new("received", false),
                new("awaiting-approval", false),
                new("waiting-information", false),
                new("manual-review", false),
                new("integration-error", false)
            ],
            [
                new("prepare-approval", "received", "awaiting-approval"),
                new("request-information", "received", "waiting-information"),
                new("request-review", "received", "manual-review"),
                new("technical-error", "received", "integration-error")
            ]);

        var assessmentPolicy = new ApprovalRoutingPolicy(
            "synthetic.work-queue-assessment-routing",
            1,
            [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);

        var processPolicy = new CaseProcessingRoutingPolicy(
            "synthetic.work-queue-process-routing",
            1,
            assessmentPolicy.Id,
            assessmentPolicy.Version,
            definition.Id,
            definition.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
                [AssessmentRoutingDisposition.Incomplete] = "request-information",
                [AssessmentRoutingDisposition.HumanReview] = "request-review"
            });

        var configuration = new CaseWorkQueueConfiguration(
            "synthetic.work-queues",
            1,
            [
                new("approval", definition.Id, definition.Version, ["awaiting-approval"]),
                new("clarification", definition.Id, definition.Version, ["waiting-information"]),
                new("review", definition.Id, definition.Version, ["manual-review"]),
                new("technical", definition.Id, definition.Version, ["integration-error"])
            ]);

        var cases = new Dictionary<string, SyntheticWorkQueueCase>(StringComparer.Ordinal);
        AddAssessed(
            cases,
            pack,
            platformVersion,
            definition,
            assessmentPolicy,
            processPolicy,
            configuration,
            "synthetic-case-approval",
            "synthetic-assessment-approval",
            TruthValue.Yes,
            EvidenceStatus.Present,
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.Zero));

        AddAssessed(
            cases,
            pack,
            platformVersion,
            definition,
            assessmentPolicy,
            processPolicy,
            configuration,
            "synthetic-case-clarification",
            "synthetic-assessment-clarification",
            TruthValue.Unknown,
            EvidenceStatus.Present,
            new DateTimeOffset(2026, 10, 3, 10, 5, 0, TimeSpan.Zero));

        AddAssessed(
            cases,
            pack,
            platformVersion,
            definition,
            assessmentPolicy,
            processPolicy,
            configuration,
            "synthetic-case-review",
            "synthetic-assessment-review",
            TruthValue.Yes,
            EvidenceStatus.Missing,
            new DateTimeOffset(2026, 10, 3, 10, 10, 0, TimeSpan.Zero));

        var technical = CaseProcessingInstance
            .Start(new CaseId("synthetic-case-technical"), 1, definition)
            .Apply(definition, 0, "technical-error");
        var technicalMembership = new CaseWorkQueueProjectionService()
            .Project(technical, configuration);
        cases.Add(
            technicalMembership.CaseId.Value,
            new(
                technicalMembership,
                Assessment: null,
                Routing: null,
                TechnicalReasonCode: "synthetic-adapter-unavailable"));

        return new(configuration, cases);
    }

    private static void AddAssessed(
        IDictionary<string, SyntheticWorkQueueCase> cases,
        KnowledgePack pack,
        string platformVersion,
        WorkflowDefinition definition,
        ApprovalRoutingPolicy assessmentPolicy,
        CaseProcessingRoutingPolicy processPolicy,
        CaseWorkQueueConfiguration configuration,
        string caseId,
        string assessmentId,
        TruthValue requestConfirmed,
        EvidenceStatus evidenceStatus,
        DateTimeOffset recordedAtUtc)
    {
        var record = new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = requestConfirmed,
                ["measurement"] = 15m,
                ["alternative_confirmed"] = TruthValue.No
            },
            new DateOnly(2026, 10, 3),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = evidenceStatus
            },
            new(
                new AssessmentId(assessmentId),
                new CaseId(caseId),
                platformVersion,
                recordedAtUtc));

        var routing = new AssessmentTriageService()
            .Route(record, assessmentPolicy);

        var initial = CaseProcessingInstance.Start(
            record.CaseId,
            1,
            definition);

        var routed = new CaseProcessingRoutingService().Apply(
            routing,
            initial,
            definition,
            processPolicy,
            expectedCaseRevision: 1,
            expectedProcessRevision: 0);

        if (routed.Status != CaseProcessingRoutingStatus.Applied)
            throw new InvalidOperationException("Synthetic work item did not enter a process queue.");

        var projection = new CaseWorkQueueProjectionService().Project(
            record,
            routing,
            routed.Process,
            configuration);

        if (projection.Status != CaseWorkQueueProjectionStatus.Assigned)
            throw new InvalidOperationException("Synthetic work item is not assigned to a queue.");

        cases.Add(
            projection.CaseId.Value,
            new(
                projection.Membership,
                record,
                routing,
                TechnicalReasonCode: null));
    }

    private static string Outcome(AssessmentOutcome outcome)
        => outcome switch
        {
            AssessmentOutcome.Supported => "SUPPORTED",
            AssessmentOutcome.NotSupported => "NOT_SUPPORTED",
            AssessmentOutcome.Incomplete => "INCOMPLETE",
            AssessmentOutcome.HumanReview => "HUMAN_REVIEW",
            AssessmentOutcome.NotApplicable => "NOT_APPLICABLE",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };

    private static string Disposition(AssessmentRoutingDisposition disposition)
        => disposition switch
        {
            AssessmentRoutingDisposition.ReadyForApproval => "READY_FOR_APPROVAL",
            AssessmentRoutingDisposition.Incomplete => "INCOMPLETE",
            AssessmentRoutingDisposition.HumanReview => "HUMAN_REVIEW",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition))
        };

    private sealed record SyntheticWorkload(
        CaseWorkQueueConfiguration Configuration,
        IReadOnlyDictionary<string, SyntheticWorkQueueCase> Cases);

    private sealed record SyntheticWorkQueueCase(
        CaseWorkQueueMembership Membership,
        AssessmentRecord? Assessment,
        AssessmentRouting? Routing,
        string? TechnicalReasonCode);
}
