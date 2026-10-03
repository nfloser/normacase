using NormaCase.Knowledge.Model;

namespace NormaCase.Application.Workflows;

public sealed record WorkflowSourceSnapshot
{
    public WorkflowSourceSnapshot(
        string id,
        string authority,
        string title,
        string documentType,
        string status,
        string? version,
        string? sourceLocation,
        DateOnly? publicationDate,
        DateOnly? validFrom,
        DateOnly? validUntil,
        DateOnly? retrievedAt,
        string? contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (validFrom is not null
            && validUntil is not null
            && validUntil < validFrom)
        {
            throw new ArgumentException(
                "Workflow source validity must not end before it starts.",
                nameof(validUntil));
        }

        if (!string.IsNullOrWhiteSpace(contentHash)
            && !IsSha256(contentHash))
        {
            throw new ArgumentException(
                "Workflow source content hash must use sha256 followed by 64 hexadecimal characters.",
                nameof(contentHash));
        }

        Id = id;
        Authority = authority;
        Title = title;
        DocumentType = documentType;
        Status = status;
        Version = version;
        SourceLocation = sourceLocation;
        PublicationDate = publicationDate;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        RetrievedAt = retrievedAt;
        ContentHash = contentHash;
    }

    public string Id { get; }

    public string Authority { get; }

    public string Title { get; }

    public string DocumentType { get; }

    public string Status { get; }

    public string? Version { get; }

    public string? SourceLocation { get; }

    public DateOnly? PublicationDate { get; }

    public DateOnly? ValidFrom { get; }

    public DateOnly? ValidUntil { get; }

    public DateOnly? RetrievedAt { get; }

    public string? ContentHash { get; }

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

    private static bool IsSha256(string value)
    {
        const string prefix = "sha256:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length != prefix.Length + 64)
        {
            return false;
        }

        return value[prefix.Length..].All(Uri.IsHexDigit);
    }
}
