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

internal sealed record SyntheticCaseSeed(AssessmentRecord? Assessment, CaseProcessingInstance Process);
internal sealed record SyntheticWorkload(WorkflowDefinition Workflow, CaseWorkQueueConfiguration Queues, IReadOnlyList<SyntheticCaseSeed> Cases)
{
    internal static SyntheticWorkload Create(IReadOnlyDictionary<string, KnowledgePack> packs, string platformVersion, bool review = false)
    {
        var workflow = new WorkflowDefinition(review ? "synthetic-review-process" : "synthetic-queue-process", review ? 1 : 2, "received",
            [new("received", false), new("awaiting-approval", false), new("waiting-information", false),
             new("manual-review", false), new("integration-error", false), new("reviewed", true)],
            [new("prepare-approval", "received", "awaiting-approval"),
             new("request-information", "received", "waiting-information"),
             new("request-review", "received", "manual-review"),
             new("technical-error", "received", "integration-error"),
             new("accept-result", "awaiting-approval", "reviewed"),
             new("override-ready", "awaiting-approval", "reviewed"),
             new("override-review", "manual-review", "reviewed")]);
        var triagePolicy = new ApprovalRoutingPolicy("synthetic-queue-triage", 1,
            [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
        var routingPolicy = new CaseProcessingRoutingPolicy(review ? "synthetic-review-routing" : "synthetic-queue-routing", review ? 1 : 2,
            triagePolicy.Id, triagePolicy.Version, workflow.Id, workflow.Version,
            new Dictionary<AssessmentRoutingDisposition, string>
            {
                [AssessmentRoutingDisposition.ReadyForApproval] = "prepare-approval",
                [AssessmentRoutingDisposition.Incomplete] = "request-information",
                [AssessmentRoutingDisposition.HumanReview] = "request-review"
            });
        var configuration = new CaseWorkQueueConfiguration(review ? "synthetic-review-queues" : "synthetic-queues", review ? 1 : 2,
            [new("approval", workflow.Id, workflow.Version, ["awaiting-approval"]),
             new("clarification", workflow.Id, workflow.Version, ["waiting-information"]),
             new("review", workflow.Id, workflow.Version, ["manual-review"]),
             new("technical", workflow.Id, workflow.Version, ["integration-error"]),
             .. (review ? new[] { new CaseWorkQueueDefinition("completed", workflow.Id, workflow.Version, ["reviewed"]) } : [])]);
        var projection = new CaseWorkQueueProjectionService();
        var items = new List<SyntheticCaseSeed>();
        foreach (var id in new[] { "demo-g-supported", "demo-g-not-supported", "demo-g-incomplete", "demo-g-review" })
        {
            var input = CaseInputJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Examples", id + ".json")));
            var record = new AssessmentRecorder().Evaluate(packs["synthetic.demo-g"], input.Facts,
                input.AssessmentDate, input.Evidence,
                new(new("assessment-" + id), new(id), platformVersion, new DateTimeOffset(2026, 10, 3, 13, 0, 0, TimeSpan.Zero)));
            var triage = new AssessmentTriageService().Route(record, triagePolicy);
            var process = CaseProcessingInstance.Start(record.CaseId, 1, workflow);
            var applied = new CaseProcessingRoutingService().Apply(triage, process, workflow, routingPolicy, 1, 0);
            if (applied.Status != CaseProcessingRoutingStatus.Applied)
                throw new InvalidOperationException("Synthetic queue fixture could not be routed.");
            projection.Project(record, triage, applied.Process, configuration);
            items.Add(new(record, applied.Process));
        }
        var technical = CaseProcessingInstance.Start(new("demo-technical"), 1, workflow)
            .Apply(workflow, 0, "technical-error");
        items.Add(new(null, technical));
        return new(workflow, configuration, items.AsReadOnly());
    }
}
