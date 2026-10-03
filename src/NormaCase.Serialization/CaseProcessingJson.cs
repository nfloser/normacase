using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Serialization;

public sealed record CaseProcessingStateData(
    string Id,
    bool IsTerminal);

public sealed record CaseProcessingTransitionData(
    string Id,
    string FromStateId,
    string ToStateId);

public sealed record CaseProcessingData(
    CaseId CaseId,
    long CaseRevision,
    string WorkflowId,
    int WorkflowVersion,
    string InitialStateId,
    IReadOnlyList<CaseProcessingStateData> States,
    IReadOnlyList<CaseProcessingTransitionData> Transitions,
    string StateId,
    long Revision);

public sealed record CaseProcessingDocument(
    int FormatVersion,
    CaseProcessingData Process);

public sealed record CaseProcessingSnapshot(
    WorkflowDefinition Definition,
    CaseProcessingInstance Process);

public static class CaseProcessingJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(
        CaseProcessingInstance process,
        WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(definition);

        ValidateDefinition(process, definition);

        var document = new CaseProcessingDocument(
            CurrentFormatVersion,
            new CaseProcessingData(
                process.CaseId,
                process.CaseRevision,
                definition.Id,
                definition.Version,
                definition.InitialStateId,
                definition.States.Select(
                    state => new CaseProcessingStateData(
                        state.Id,
                        state.IsTerminal)).ToArray(),
                definition.Transitions.Select(
                    transition => new CaseProcessingTransitionData(
                        transition.Id,
                        transition.FromStateId,
                        transition.ToStateId)).ToArray(),
                process.StateId,
                process.Revision));

        return JsonSerializer.Serialize(
            document,
            InterchangeJson.Options);
    }

    public static CaseProcessingSnapshot Deserialize(string json)
    {
        try
        {
            var document =
                InterchangeJson.Read<CaseProcessingDocument>(json);

            if (document.FormatVersion != CurrentFormatVersion)
                throw new JsonException(
                    "Unsupported case-processing document format.");

            var data = document.Process;
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(data.States);
            ArgumentNullException.ThrowIfNull(data.Transitions);

            if (data.CaseId.IsEmpty
                || data.CaseRevision < 1
                || data.WorkflowVersion < 1
                || data.Revision < 0
                || string.IsNullOrWhiteSpace(data.WorkflowId)
                || string.IsNullOrWhiteSpace(data.InitialStateId)
                || string.IsNullOrWhiteSpace(data.StateId)
                || data.States.Any(state => state is null)
                || data.Transitions.Any(transition => transition is null))
            {
                throw new JsonException(
                    "Invalid case-processing document.");
            }

            var definition = new WorkflowDefinition(
                data.WorkflowId,
                data.WorkflowVersion,
                data.InitialStateId,
                data.States.Select(
                    state => new WorkflowStateDefinition(
                        state.Id,
                        state.IsTerminal)),
                data.Transitions.Select(
                    transition => new WorkflowTransitionDefinition(
                        transition.Id,
                        transition.FromStateId,
                        transition.ToStateId)));

            var process = CaseProcessingInstance.Restore(
                data.CaseId,
                data.CaseRevision,
                definition,
                data.StateId,
                data.Revision);

            return new(definition, process);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidOperationException
                or OverflowException)
        {
            throw new JsonException(
                "Invalid case-processing document.");
        }
    }

    private static void ValidateDefinition(
        CaseProcessingInstance process,
        WorkflowDefinition definition)
    {
        if (!string.Equals(
                process.WorkflowId,
                definition.Id,
                StringComparison.Ordinal)
            || process.WorkflowVersion != definition.Version)
        {
            throw new ArgumentException(
                "Workflow definition identity does not match case processing.",
                nameof(definition));
        }

        _ = process.IsTerminal(definition);
    }
}
