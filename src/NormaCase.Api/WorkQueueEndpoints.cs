using System.Globalization;
using NormaCase.Application.Assessments;
using NormaCase.Application.Processing;
using NormaCase.Application.Triage;
using NormaCase.Application.WorkQueues;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Serialization;

namespace NormaCase.Api;

// Read-only synthetic fixtures, rebuilt deterministically for each demo host.
internal static class WorkQueueEndpoints
{
    private const int ScenarioCopies = 20;
    private const int PreviewLimitPerQueue = 5;

    internal static void Map(WebApplication app, IReadOnlyDictionary<string, KnowledgePack> packs, string platformVersion)
    {
        var workflow = new WorkflowDefinition("synthetic-queue-process", 1, "received",
            [new("received", false), new("awaiting-approval", false), new("waiting-information", false),
             new("manual-review", false), new("integration-error", false)],
            [new("prepare-approval", "received", "awaiting-approval"),
             new("request-information", "received", "waiting-information"),
             new("request-review", "received", "manual-review"),
             new("technical-error", "received", "integration-error")]);
        var triagePolicy = new ApprovalRoutingPolicy("synthetic-queue-triage", 1,
            [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
        var routingPolicy = new CaseProcessingRoutingPolicy("synthetic-queue-routing", 1,
            triagePolicy.Id, triagePolicy.Version, workflow.Id, workflow.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
                [AssessmentRoutingDisposition.Incomplete] = "request-information",
                [AssessmentRoutingDisposition.HumanReview] = "request-review"
            });
        var configuration = new CaseWorkQueueConfiguration("synthetic-queues", 1,
            [new("approval", workflow.Id, workflow.Version, ["awaiting-approval"]),
             new("clarification", workflow.Id, workflow.Version, ["waiting-information"]),
             new("review", workflow.Id, workflow.Version, ["manual-review"]),
             new("technical", workflow.Id, workflow.Version, ["integration-error"])]);
        var projection = new CaseWorkQueueProjectionService();
        var recorder = new AssessmentRecorder();
        var triageService = new AssessmentTriageService();
        var routingService = new CaseProcessingRoutingService();
        var scenarioTemplates = new[]
        {
            "demo-g-supported",
            "demo-g-not-supported",
            "demo-g-incomplete",
            "demo-g-review"
        }.Select(id => new
        {
            Id = id,
            Input = CaseInputJson.Deserialize(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", id + ".json")))
        }).ToArray();

        var items = new List<Detail>(ScenarioCopies * 5);
        var recordedAt = new DateTimeOffset(2026, 10, 3, 13, 0, 0, TimeSpan.Zero);
        for (var copy = 1; copy <= ScenarioCopies; copy++)
        {
            foreach (var template in scenarioTemplates)
            {
                var caseId = ReplicaId(template.Id, copy);
                var record = recorder.Evaluate(packs["synthetic.demo-g"], template.Input.Facts,
                    template.Input.AssessmentDate, template.Input.Evidence,
                    new(new("assessment-" + caseId), new(caseId), platformVersion,
                        recordedAt.AddMinutes(items.Count)));
                var triage = triageService.Route(record, triagePolicy);
                var process = CaseProcessingInstance.Start(record.CaseId, 1, workflow);
                var applied = routingService.Apply(triage, process, workflow, routingPolicy, 1, 0);
                if (applied.Status != CaseProcessingRoutingStatus.Applied)
                    throw new InvalidOperationException("Synthetic queue fixture could not be routed.");
                var enriched = projection.Project(record, triage, applied.Process, configuration);
                items.Add(Create(enriched.Membership, record));
            }

            var technicalCaseId = ReplicaId("demo-technical", copy);
            var technical = CaseProcessingInstance.Start(new(technicalCaseId), 1, workflow)
                .Apply(workflow, 0, "technical-error");
            items.Add(Create(projection.Project(technical, configuration), null));
        }

        var details = items.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
        app.MapGet("/api/work-queues", () => Results.Json(new
        {
            configurationId = configuration.Id,
            configurationVersion = configuration.Version,
            totalCases = items.Count,
            previewLimit = PreviewLimitPerQueue,
            queues = configuration.Queues.Select(queue =>
            {
                var queueItems = items.Where(item => item.QueueId == queue.QueueId).ToArray();
                return new
                {
                    queueId = queue.QueueId,
                    totalCount = queueItems.Length,
                    items = queueItems.Take(PreviewLimitPerQueue).Select(item => new
                    {
                        item.CaseId, item.CaseRevision, item.WorkflowId, item.WorkflowVersion,
                        item.StateId, item.ProcessRevision, item.AssessmentId
                    }).ToArray()
                };
            }).ToArray()
        }));
        app.MapGet("/api/work-cases/{caseId}", (string caseId) => details.TryGetValue(caseId, out var detail)
            ? Results.Json(detail) : DemoHost.Error("unknown_work_case", 404));
    }

    private static string ReplicaId(string baseId, int copy)
        => copy == 1 ? baseId : $"{baseId}-{copy:00}";

    private static Detail Create(CaseWorkQueueMembership membership, AssessmentRecord? record)
        => new(membership.CaseId.ToString(), membership.CaseRevision.ToString(CultureInfo.InvariantCulture),
            membership.QueueId!, membership.WorkflowId, membership.WorkflowVersion, membership.StateId,
            membership.ProcessRevision.ToString(CultureInfo.InvariantCulture), record?.AssessmentId.ToString(),
            record?.KnowledgePackId, record?.RecordedAtUtc,
            record is null ? null : AssessmentJson.Serialize(record.Result, record.PlatformVersion),
            record?.Input.Evidence.ToDictionary(item => item.Key, item => item.Value.ToString().ToUpperInvariant())
                ?? new Dictionary<string, string>());

    private sealed record Detail(string CaseId, string CaseRevision, string QueueId, string WorkflowId,
        int WorkflowVersion, string StateId, string ProcessRevision, string? AssessmentId, string? PackId,
        DateTimeOffset? RecordedAtUtc, string? AssessmentJson, IReadOnlyDictionary<string, string> Evidence);
}
