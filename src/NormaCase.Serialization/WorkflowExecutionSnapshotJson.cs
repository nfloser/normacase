using System.Text.Json;
using NormaCase.Application.Workflows;

namespace NormaCase.Serialization;

public sealed record WorkflowExecutionSnapshotDocument(
    int FormatVersion,
    WorkflowExecutionSnapshot Snapshot);

public static class WorkflowExecutionSnapshotJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(
        WorkflowExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return JsonSerializer.Serialize(
            new WorkflowExecutionSnapshotDocument(
                CurrentFormatVersion,
                snapshot),
            InterchangeJson.Options);
    }

    public static WorkflowExecutionSnapshot Deserialize(
        string json)
    {
        try
        {
            var document =
                InterchangeJson.Read<WorkflowExecutionSnapshotDocument>(
                    json);

            if (document.FormatVersion != CurrentFormatVersion)
            {
                throw new JsonException(
                    "Unsupported workflow execution snapshot format.");
            }

            _ = new WorkflowExecutionService().Restore(
                document.Snapshot);

            return document.Snapshot;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidOperationException)
        {
            throw new JsonException(
                "Invalid workflow execution snapshot.",
                exception);
        }
    }
}
