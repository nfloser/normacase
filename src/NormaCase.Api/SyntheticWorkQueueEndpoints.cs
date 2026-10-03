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
    internal static void Map(
        WebApplication app,
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        var catalog = SyntheticWorkQueueCatalog.Build(packs, platformVersion);

        app.MapGet("/api/work-queues", () => Results.Json(catalog.Summary));

        app.MapGet("/api/work-items/{caseId}", (string caseId) =>
            catalog.TryGet(caseId, out var item)
                ? Results.Json(item)
                : DemoHost.Error("unknown_work_item", 404));
    }
}

internal sealed class SyntheticWorkQueueCatalog
{
    private const string PackId = "synthetic.demo-g";
    private static readonly DateOnly AssessmentDate = new(2026, 10, 3);
    private readonly IReadOnlyDictionary<string, SyntheticWorkItemDetail> items;

    private SyntheticWorkQueueCatalog(
        SyntheticWorkQueueSummary summary,
        IReadOnlyDictionary<string, SyntheticWorkItemDetail> items)
    {
        Summary = summary;
        this.items = items;
    }

    internal SyntheticWorkQueueSummary Summary { get; }

    internal bool TryGet(string caseId, out SyntheticWorkItemDetail item)
        => items.TryGetValue(caseId, out item!);

    internal static SyntheticWorkQueueCatalog Build(
        IReadOnlyDictionary<string, KnowledgePack> packs,
        string platformVersion)
    {
        if (!packs.TryGetValue(PackId, out var pack)
            || pack.Manifest.ValidationLevel != "SYNTHETIC")
        {
            throw new InvalidOperationException(
                "Synthetic work queues require the bundled pitch Knowledge Pack.");
        }

        var workflow = Workflow();
        var assessmentPolicy = new ApprovalRoutingPolicy(
            "synthetic-demo-assessment-routing",
            1,
            [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
        var processPolicy = new CaseProcessingRoutingPolicy(
            "synthetic-demo-process-routing",
            1,
            assessmentPolicy.Id,
            assessmentPolicy.Version,
            workflow.Id,
            workflow.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
                [AssessmentRoutingDisposition.Incomplete] = "request-information",
                [AssessmentRoutingDisposition.HumanReview] = "request-review"
            });
        var configuration = Configuration(workflow);

        var recorder = new AssessmentRecorder();
        var triage = new AssessmentTriageService();
        var processRouting = new CaseProcessingRoutingService();
        var queues = new CaseWorkQueueProjectionService();

        var details = new[]
        {
            BuildAssessed(
                "synthetic-approval-001",
                "synthetic-assessment-approval-001",
                pack,
                platformVersion,
                new Dictionary<string, CaseValue>(StringComparer.Ordinal)
                {
                    ["request_complete"] = TruthValue.Yes,
                    ["criteria_confirmed"] = TruthValue.Yes
                },
                new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal)
                {
                    ["supporting_document"] = EvidenceStatus.Present
                },
                new DateTimeOffset(2026, 10, 3, 14, 0, 0, TimeSpan.Zero),
                workflow,
                assessmentPolicy,
                processPolicy,
                configuration,
                recorder,
                triage,
                processRouting,
                queues),
            BuildAssessed(
                "synthetic-information-001",
                "synthetic-assessment-information-001",
                pack,
                platformVersion,
                new Dictionary<string, CaseValue>(StringComparer.Ordinal)
                {
                    ["criteria_confirmed"] = TruthValue.Yes
                },
                new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal)
                {
                    ["supporting_document"] = EvidenceStatus.Present
                },
                new DateTimeOffset(2026, 10, 3, 14, 1, 0, TimeSpan.Zero),
                workflow,
                assessmentPolicy,
                processPolicy,
                configuration,
                recorder,
                triage,
                processRouting,
                queues),
            BuildAssessed(
                "synthetic-review-001",
                "synthetic-assessment-review-001",
                pack,
                platformVersion,
                new Dictionary<string, CaseValue>(StringComparer.Ordinal)
                {
                    ["request_complete"] = TruthValue.Yes,
                    ["criteria_confirmed"] = TruthValue.Yes
                },
                new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal)
                {
                    ["supporting_document"] = EvidenceStatus.Missing
                },
                new DateTimeOffset(2026, 10, 3, 14, 2, 0, TimeSpan.Zero),
                workflow,
                assessmentPolicy,
                processPolicy,
                configuration,
                recorder,
                triage,
                processRouting,
                queues),
            BuildTechnical(workflow, configuration, queues)
        };

        var byCase = details.ToDictionary(
            item => item.CaseId,
            item => item,
            StringComparer.Ordinal);

        var summary = new SyntheticWorkQueueSummary(
            configuration.Id,
            configuration.Version,
            configuration.Queues.Select(queue => new SyntheticQueueSummary(
                queue.QueueId,
                details
                    .Where(item => string.Equals(item.QueueId, queue.QueueId, StringComparison.Ordinal))
                    .Select(item => new SyntheticWorkItemSummary(
                        item.CaseId,
                        item.CaseRevision,
                        item.WorkflowId,
                        item.WorkflowVersion,
                        item.StateId,
                        item.ProcessRevision,
                        item.Assessment is not null))
                    .ToArray()))
                .ToArray());

        return new(summary, byCase);
    }

    private static SyntheticWorkItemDetail BuildAssessed(
        string caseId,
        string assessmentId,
        KnowledgePack pack,
        string platformVersion,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence,
        DateTimeOffset recordedAtUtc,
        WorkflowDefinition workflow,
        ApprovalRoutingPolicy assessmentPolicy,
        CaseProcessingRoutingPolicy processPolicy,
        CaseWorkQueueConfiguration configuration,
        AssessmentRecorder recorder,
        AssessmentTriageService triage,
        CaseProcessingRoutingService processRouting,
        CaseWorkQueueProjectionService queues)
    {
        var record = recorder.Evaluate(
            pack,
            facts,
            AssessmentDate,
            evidence,
            new AssessmentExecutionContext(
                new AssessmentId(assessmentId),
                new CaseId(caseId),
                platformVersion,
                recordedAtUtc));

        var routing = triage.Route(record, assessmentPolicy);
        var initial = CaseProcessingInstance.Start(record.CaseId, 1, workflow);
        var routed = processRouting.Apply(
            routing,
            initial,
            workflow,
            processPolicy,
            expectedCaseRevision: 1,
            expectedProcessRevision: 0);

        if (routed.Status != CaseProcessingRoutingStatus.Applied)
            throw new InvalidOperationException(
                "Synthetic work-queue fixture did not produce a process transition.");

        var projection = queues.Project(
            record,
            routing,
            routed.Process,
            configuration);

        if (projection.Status != CaseWorkQueueProjectionStatus.Assigned
            || projection.QueueId is null)
        {
            throw new InvalidOperationException(
                "Synthetic work-queue fixture is not assigned to a queue.");
        }

        return new SyntheticWorkItemDetail(
            projection.CaseId.Value,
            projection.CaseRevision,
            projection.WorkflowId,
            projection.WorkflowVersion,
            projection.StateId,
            projection.ProcessRevision,
            projection.QueueId,
            "RECORDED",
            new SyntheticAssessmentDetail(
                projection.AssessmentId.Value,
                Outcome(record.Result.Outcome),
                Routing(routing.Disposition),
                record.KnowledgePackId,
                record.Result.KnowledgeRelease,
                record.Result.AssessmentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                record.Input.Evidence
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new SyntheticEvidenceDetail(
                        item.Key,
                        Evidence(item.Value)))
                    .ToArray(),
                AssessmentJson.Serialize(record.Result, record.PlatformVersion)));
    }

    private static SyntheticWorkItemDetail BuildTechnical(
        WorkflowDefinition workflow,
        CaseWorkQueueConfiguration configuration,
        CaseWorkQueueProjectionService queues)
    {
        var process = CaseProcessingInstance
            .Start(new CaseId("synthetic-technical-001"), 1, workflow)
            .Apply(workflow, 0, "technical-error");
        var membership = queues.Project(process, configuration);

        if (membership.Status != CaseWorkQueueProjectionStatus.Assigned
            || membership.QueueId is null)
        {
            throw new InvalidOperationException(
                "Synthetic technical work item is not assigned to a queue.");
        }

        return new SyntheticWorkItemDetail(
            membership.CaseId.Value,
            membership.CaseRevision,
            membership.WorkflowId,
            membership.WorkflowVersion,
            membership.StateId,
            membership.ProcessRevision,
            membership.QueueId,
            "NOT_RECORDED",
            Assessment: null);
    }

    private static WorkflowDefinition Workflow()
        => new(
            "synthetic-demo-case-process",
            1,
            "received",
            [
                new WorkflowStateDefinition("received", false),
                new WorkflowStateDefinition("awaiting-approval", false),
                new WorkflowStateDefinition("waiting-information", false),
                new WorkflowStateDefinition("manual-review", false),
                new WorkflowStateDefinition("integration-error", false)
            ],
            [
                new WorkflowTransitionDefinition("prepare-approval", "received", "awaiting-approval"),
                new WorkflowTransitionDefinition("request-information", "received", "waiting-information"),
                new WorkflowTransitionDefinition("request-review", "received", "manual-review"),
                new WorkflowTransitionDefinition("technical-error", "received", "integration-error")
            ]);

    private static CaseWorkQueueConfiguration Configuration(
        WorkflowDefinition workflow)
        => new(
            "synthetic-demo-work-queues",
            1,
            [
                new CaseWorkQueueDefinition(
                    "approval",
                    workflow.Id,
                    workflow.Version,
                    ["awaiting-approval"]),
                new CaseWorkQueueDefinition(
                    "clarification",
                    workflow.Id,
                    workflow.Version,
                    ["waiting-information"]),
                new CaseWorkQueueDefinition(
                    "review",
                    workflow.Id,
                    workflow.Version,
                    ["manual-review"]),
                new CaseWorkQueueDefinition(
                    "technical",
                    workflow.Id,
                    workflow.Version,
                    ["integration-error"])
            ]);

    private static string Outcome(AssessmentOutcome value)
        => value switch
        {
            AssessmentOutcome.Supported => "SUPPORTED",
            AssessmentOutcome.NotSupported => "NOT_SUPPORTED",
            AssessmentOutcome.Incomplete => "INCOMPLETE",
            AssessmentOutcome.HumanReview => "HUMAN_REVIEW",
            AssessmentOutcome.NotApplicable => "NOT_APPLICABLE",
            _ => throw new InvalidOperationException("Unsupported assessment outcome.")
        };

    private static string Routing(AssessmentRoutingDisposition value)
        => value switch
        {
            AssessmentRoutingDisposition.ReadyForApproval => "READY_FOR_APPROVAL",
            AssessmentRoutingDisposition.Incomplete => "INCOMPLETE",
            AssessmentRoutingDisposition.HumanReview => "HUMAN_REVIEW",
            _ => throw new InvalidOperationException("Unsupported routing disposition.")
        };

    private static string Evidence(EvidenceStatus value)
        => value switch
        {
            EvidenceStatus.Missing => "MISSING",
            EvidenceStatus.Present => "PRESENT",
            EvidenceStatus.Conflicting => "CONFLICTING",
            _ => throw new InvalidOperationException("Unsupported evidence status.")
        };
}

internal sealed record SyntheticWorkQueueSummary(
    string ConfigurationId,
    int ConfigurationVersion,
    IReadOnlyList<SyntheticQueueSummary> Queues);

internal sealed record SyntheticQueueSummary(
    string QueueId,
    IReadOnlyList<SyntheticWorkItemSummary> Items);

internal sealed record SyntheticWorkItemSummary(
    string CaseId,
    long CaseRevision,
    string WorkflowId,
    int WorkflowVersion,
    string StateId,
    long ProcessRevision,
    bool HasAssessment);

internal sealed record SyntheticWorkItemDetail(
    string CaseId,
    long CaseRevision,
    string WorkflowId,
    int WorkflowVersion,
    string StateId,
    long ProcessRevision,
    string QueueId,
    string AssessmentStatus,
    SyntheticAssessmentDetail? Assessment);

internal sealed record SyntheticAssessmentDetail(
    string AssessmentId,
    string Outcome,
    string RoutingDisposition,
    string KnowledgePackId,
    string KnowledgeRelease,
    string AssessmentDate,
    IReadOnlyList<SyntheticEvidenceDetail> Evidence,
    string AssessmentJson);

internal sealed record SyntheticEvidenceDetail(
    string Id,
    string Status);
