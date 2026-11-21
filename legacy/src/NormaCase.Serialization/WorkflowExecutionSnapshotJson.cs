using System.Text.Json;
using NormaCase.Application.Workflows;

namespace NormaCase.Serialization;

public sealed record WorkflowSourceSnapshotData(
    string Id,
    string Authority,
    string Title,
    string DocumentType,
    string Status,
    string? Version,
    string? SourceLocation,
    DateOnly? PublicationDate,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    DateOnly? RetrievedAt,
    string? ContentHash);

public sealed record WorkflowStateSnapshotData(
    string Id,
    bool IsTerminal);

public sealed record WorkflowTransitionSnapshotData(
    string Id,
    string FromStateId,
    string ToStateId);

public sealed record WorkflowExecutionSnapshotData(
    string KnowledgePackId,
    string KnowledgeRelease,
    WorkflowSourceSnapshotData Source,
    string WorkflowId,
    int WorkflowVersion,
    string InitialStateId,
    IReadOnlyList<WorkflowStateSnapshotData> States,
    IReadOnlyList<WorkflowTransitionSnapshotData> Transitions,
    string StateId,
    long Revision);

public sealed record WorkflowExecutionSnapshotDocument(
    int FormatVersion,
    WorkflowExecutionSnapshotData Snapshot);

public static class WorkflowExecutionSnapshotJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(
        WorkflowExecutionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _ = new WorkflowExecutionService().Restore(snapshot);

        return JsonSerializer.Serialize(
            new WorkflowExecutionSnapshotDocument(
                CurrentFormatVersion,
                ToData(snapshot)),
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

            var snapshot = FromData(document.Snapshot);

            _ = new WorkflowExecutionService().Restore(snapshot);

            return snapshot;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw new JsonException(
                "Invalid workflow execution snapshot.");
        }
        catch (InvalidOperationException)
        {
            throw new JsonException(
                "Invalid workflow execution snapshot.");
        }
    }

    internal static WorkflowExecutionSnapshotData ToData(
        WorkflowExecutionSnapshot snapshot)
        => new(
            snapshot.KnowledgePackId,
            snapshot.KnowledgeRelease,
            new WorkflowSourceSnapshotData(
                snapshot.Source.Id,
                snapshot.Source.Authority,
                snapshot.Source.Title,
                snapshot.Source.DocumentType,
                snapshot.Source.Status,
                snapshot.Source.Version,
                snapshot.Source.SourceLocation,
                snapshot.Source.PublicationDate,
                snapshot.Source.ValidFrom,
                snapshot.Source.ValidUntil,
                snapshot.Source.RetrievedAt,
                snapshot.Source.ContentHash),
            snapshot.WorkflowId,
            snapshot.WorkflowVersion,
            snapshot.InitialStateId,
            snapshot.States.Select(
                state => new WorkflowStateSnapshotData(
                    state.Id,
                    state.IsTerminal)).ToArray(),
            snapshot.Transitions.Select(
                transition => new WorkflowTransitionSnapshotData(
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId)).ToArray(),
            snapshot.StateId,
            snapshot.Revision);

    internal static WorkflowExecutionSnapshot FromData(
        WorkflowExecutionSnapshotData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(data.States);
        ArgumentNullException.ThrowIfNull(data.Transitions);

        if (data.States.Any(state => state is null)
            || data.Transitions.Any(
                transition => transition is null))
        {
            throw new JsonException(
                "Workflow graph entries are required.");
        }

        var source = data.Source;
        ArgumentNullException.ThrowIfNull(source);

        return new(
            data.KnowledgePackId,
            data.KnowledgeRelease,
            new WorkflowSourceSnapshot(
                source.Id,
                source.Authority,
                source.Title,
                source.DocumentType,
                source.Status,
                source.Version,
                source.SourceLocation,
                source.PublicationDate,
                source.ValidFrom,
                source.ValidUntil,
                source.RetrievedAt,
                source.ContentHash),
            data.WorkflowId,
            data.WorkflowVersion,
            data.InitialStateId,
            data.States.Select(
                state => new WorkflowStateSnapshot(
                    state.Id,
                    state.IsTerminal)),
            data.Transitions.Select(
                transition => new WorkflowTransitionSnapshot(
                    transition.Id,
                    transition.FromStateId,
                    transition.ToStateId)),
            data.StateId,
            data.Revision);
    }
}
