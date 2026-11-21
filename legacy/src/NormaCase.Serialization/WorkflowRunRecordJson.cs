using System.Text.Json;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Serialization;

public sealed record WorkflowRunEventData(
    string? TransitionId,
    string ActorId,
    DateTimeOffset RecordedAtUtc,
    string Reason,
    WorkflowExecutionSnapshotData Snapshot);

public sealed record WorkflowRunRecordData(
    string RunId,
    string CaseId,
    string PlatformVersion,
    IReadOnlyList<WorkflowRunEventData> History);

public sealed record WorkflowRunRecordDocument(int FormatVersion, WorkflowRunRecordData Run);

public static class WorkflowRunRecordJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(WorkflowRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var json = JsonSerializer.Serialize(new WorkflowRunRecordDocument(
            CurrentFormatVersion,
            new WorkflowRunRecordData(run.RunId.Value, run.CaseId.Value, run.PlatformVersion,
                run.History.Select(item => new WorkflowRunEventData(
                    item.TransitionId, item.ActorId, item.RecordedAtUtc, item.Reason,
                    WorkflowExecutionSnapshotJson.ToData(item.Snapshot))).ToArray())), InterchangeJson.Options);

        if (json.Length > AssessmentJson.MaximumJsonCharacters)
            throw new JsonException("Workflow run exceeds the supported interchange size.");
        return json;
    }

    public static WorkflowRunRecord Deserialize(string json)
    {
        try
        {
            var document = InterchangeJson.Read<WorkflowRunRecordDocument>(json);
            if (document.FormatVersion != CurrentFormatVersion)
                throw new JsonException("Unsupported workflow run record format.");
            ArgumentNullException.ThrowIfNull(document.Run);
            ArgumentNullException.ThrowIfNull(document.Run.History);
            if (document.Run.History.Any(item => item is null))
                throw new JsonException("Workflow history entries are required.");

            return new WorkflowRunRecord(
                new WorkflowRunId(document.Run.RunId), new CaseId(document.Run.CaseId),
                document.Run.PlatformVersion,
                document.Run.History.Select(item => new WorkflowRunEvent(
                    item.TransitionId, item.ActorId, item.RecordedAtUtc, item.Reason,
                    WorkflowExecutionSnapshotJson.FromData(item.Snapshot))));
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            // Imported identity, actor and free-text reason are not exception diagnostics.
            throw new JsonException("Invalid workflow run record.");
        }
    }
}
