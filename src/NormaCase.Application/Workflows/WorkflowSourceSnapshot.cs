using NormaCase.Knowledge.Model;

namespace NormaCase.Application.Workflows;

public sealed record WorkflowSourceSnapshot(
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
    string? ContentHash)
{
    internal static WorkflowSourceSnapshot Copy(
        SourceDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new(
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
            source.ContentHash);
    }
}
