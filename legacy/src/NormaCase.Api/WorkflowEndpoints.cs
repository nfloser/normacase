using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Serialization;

namespace NormaCase.Api;

internal static class WorkflowEndpoints
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16
    };

    internal static void Map(WebApplication app, IReadOnlyDictionary<string, KnowledgePack> packs,
        IReadOnlyDictionary<string, LoadedPresentation> presentations, string platformVersion)
    {
        app.MapPost("/api/workflows/{packId}/start", (string packId, HttpRequest request) =>
        {
            if (!packs.TryGetValue(packId, out var pack))
                return Task.FromResult(DemoHost.Error("unknown_pack", 404));
            return DemoHost.HandleJson(request, json =>
            {
                var command = Read<StartCommand>(json);
                var execution = new WorkflowExecutionService().Start(pack, command.WorkflowId, command.WorkflowVersion);
                var run = new WorkflowRunService().Start(execution,
                    new WorkflowRunId(command.RunId), new CaseId(command.CaseId), platformVersion,
                    command.ActorId, command.RecordedAtUtc, command.Reason);
                return Reply(run, presentations);
            });
        });

        app.MapPost("/api/workflows/advance", (HttpRequest request) => DemoHost.HandleJson(request, json =>
        {
            var command = Read<ApplyCommand>(json);
            var run = WorkflowRunRecordJson.Deserialize(command.RunJson);
            VerifyInstalled(run, packs, platformVersion);
            var next = new WorkflowRunService().Apply(run, command.ExpectedRevision, command.TransitionId,
                command.ActorId, command.RecordedAtUtc, command.Reason);
            return Reply(next, presentations);
        }));

        app.MapPost("/api/workflows/verify", (HttpRequest request) => DemoHost.HandleJson(request, json =>
        {
            var run = WorkflowRunRecordJson.Deserialize(json);
            VerifyInstalled(run, packs, platformVersion);
            return Reply(run, presentations);
        }));
    }

    private static void VerifyInstalled(WorkflowRunRecord run, IReadOnlyDictionary<string, KnowledgePack> packs, string platformVersion)
    {
        if (run.PlatformVersion != platformVersion
            || !packs.TryGetValue(run.Current.KnowledgePackId, out var pack)
            || pack.Manifest.ValidationLevel != "SYNTHETIC"
            || pack.Manifest.ReleaseId != run.Current.KnowledgeRelease
            || !pack.Workflows.Any(item => item.Id == run.Current.WorkflowId && item.Version == run.Current.WorkflowVersion))
            throw new WorkflowCatalogMismatchException();

        var service = new WorkflowExecutionService();
        var original = service.Capture(service.Start(pack, run.Current.WorkflowId, run.Current.WorkflowVersion));
        if (!string.Equals(WorkflowExecutionSnapshotJson.Serialize(original),
                WorkflowExecutionSnapshotJson.Serialize(run.History[0].Snapshot), StringComparison.Ordinal))
            throw new WorkflowCatalogMismatchException();
    }

    private static IResult Reply(WorkflowRunRecord run, IReadOnlyDictionary<string, LoadedPresentation> presentations)
    {
        var json = WorkflowRunRecordJson.Serialize(run);
        // Leave room for the escaped run JSON and labelled envelope in later requests.
        if (Encoding.UTF8.GetByteCount(json) > DemoHost.MaximumBodyBytes / 4)
            return DemoHost.Error("input_too_large", 413);
        var labels = presentations[run.Current.KnowledgePackId].Workflows.Single(
            item => item.Id == run.Current.WorkflowId && item.Version == run.Current.WorkflowVersion);
        var execution = new WorkflowExecutionService().Restore(run.Current);
        return Results.Json(new
        {
            runJson = json,
            view = new
            {
                runId = run.RunId.Value, caseId = run.CaseId.Value, run.PlatformVersion,
                run.Current.KnowledgePackId, run.Current.KnowledgeRelease,
                workflowId = labels.Id, workflowVersion = labels.Version, workflowLabel = labels.Label,
                revision = run.Current.Revision.ToString(CultureInfo.InvariantCulture),
                stateLabel = labels.States[run.Current.StateId],
                terminal = execution.Instance.IsTerminal(execution.Definition),
                transitions = execution.Definition.Transitions.Where(item => item.FromStateId == run.Current.StateId)
                    .Select(item => new { id = item.Id, label = labels.Transitions[item.Id] }).ToArray(),
                history = run.History.Select(item => new
                {
                    revision = item.Snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                    stateLabel = labels.States[item.Snapshot.StateId],
                    transitionLabel = item.TransitionId is null ? ApiMessages.Get("workflow_created") : labels.Transitions[item.TransitionId],
                    item.ActorId, item.RecordedAtUtc, item.Reason
                }).ToArray()
            }
        });
    }

    private static T Read<T>(string json) where T : class
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Workflow command must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!names.Add(property.Name))
                throw new JsonException("Duplicate workflow command property.");
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("Workflow command is required.");
    }

    private sealed record StartCommand(string WorkflowId, int WorkflowVersion, string RunId, string CaseId,
        string ActorId, DateTimeOffset RecordedAtUtc, string Reason);
    private sealed record ApplyCommand(string RunJson, long ExpectedRevision, string TransitionId,
        string ActorId, DateTimeOffset RecordedAtUtc, string Reason);
}

internal sealed class WorkflowCatalogMismatchException : Exception { }
