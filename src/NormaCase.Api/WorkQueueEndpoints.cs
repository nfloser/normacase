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
    internal static void Map(WebApplication app, IReadOnlyDictionary<string, KnowledgePack> packs, string platformVersion)
    {
        var workload = SyntheticWorkload.Create(packs, platformVersion);
        var configuration = workload.Queues;
        var items = workload.Cases.Select(seed => Create(new CaseWorkQueueProjectionService().Project(seed.Process, configuration), seed.Assessment)).ToArray();
        var details = items.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
        app.MapGet("/api/work-queues", () => Results.Json(new
        {
            configurationId = configuration.Id, configurationVersion = configuration.Version,
            queues = configuration.Queues.Select(queue => new
            {
                queueId = queue.QueueId,
                items = items.Where(item => item.QueueId == queue.QueueId).Select(item => new
                {
                    item.CaseId, item.CaseRevision, item.WorkflowId, item.WorkflowVersion,
                    item.StateId, item.ProcessRevision, item.AssessmentId
                }).ToArray()
            }).ToArray()
        }));
        app.MapGet("/api/work-cases/{caseId}", (string caseId) => details.TryGetValue(caseId, out var detail)
            ? Results.Json(detail) : DemoHost.Error("unknown_work_case", 404));
    }

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
