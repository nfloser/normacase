using System.Text.Json;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Serialization;

public sealed record CaseProcessingInstanceDocument(
    int FormatVersion,
    CaseId CaseId,
    long CaseRevision,
    string WorkflowId,
    int WorkflowVersion,
    string StateId,
    long ProcessRevision);

public static class CaseProcessingInstanceJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(CaseProcessingInstance process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return JsonSerializer.Serialize(
            new CaseProcessingInstanceDocument(
                CurrentFormatVersion,
                process.CaseId,
                process.CaseRevision,
                process.WorkflowId,
                process.WorkflowVersion,
                process.StateId,
                process.Revision),
            InterchangeJson.Options);
    }

    public static CaseProcessingInstance Deserialize(string json)
    {
        try
        {
            var document =
                InterchangeJson.Read<CaseProcessingInstanceDocument>(json);

            if (document.FormatVersion != CurrentFormatVersion
                || document.CaseId.IsEmpty
                || document.CaseRevision < 1
                || string.IsNullOrWhiteSpace(document.WorkflowId)
                || document.WorkflowVersion < 1
                || string.IsNullOrWhiteSpace(document.StateId)
                || document.ProcessRevision < 0)
            {
                throw new JsonException(
                    "Invalid case processing snapshot.");
            }

            // The persisted process carries opaque workflow identity/state only.
            // A one-state definition is sufficient to rebuild that structural snapshot;
            // later operations must still supply the exact versioned workflow definition.
            var structuralDefinition = new WorkflowDefinition(
                document.WorkflowId,
                document.WorkflowVersion,
                document.StateId,
                [new WorkflowStateDefinition(document.StateId, false)],
                []);

            return CaseProcessingInstance.Restore(
                document.CaseId,
                document.CaseRevision,
                structuralDefinition,
                document.StateId,
                document.ProcessRevision);
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
                "Invalid case processing snapshot.",
                exception);
        }
    }
}
