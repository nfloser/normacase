namespace NormaCase.Application.Authorization;

public sealed record IdentityAccessState(
    string ActorId,
    long Revision,
    bool Suspended);

public sealed record IdentityAccessAuditEntry(
    string ActorId,
    long Revision,
    bool Suspended,
    string AdministratorActorId,
    DateTimeOffset ChangedAtUtc,
    string Reason);

public sealed record IdentityAccessChange(
    string ActorId,
    long ExpectedRevision,
    bool Suspended,
    string AdministratorActorId,
    DateTimeOffset ChangedAtUtc,
    string Reason);

public interface IIdentityAccessAdministrationStore
{
    Task<IdentityAccessState> LoadAsync(
        string actorId,
        CancellationToken cancellationToken = default);

    Task<IdentityAccessState> ChangeAsync(
        IdentityAccessChange change,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentityAccessAuditEntry>> LoadHistoryAsync(
        string actorId,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class IdentityAccessConflictException : Exception
{
    public IdentityAccessConflictException()
        : base("Identity access state changed concurrently.") { }
}

public sealed class IdentityAccessStorageException : Exception
{
    public IdentityAccessStorageException()
        : base("Identity access storage operation failed.") { }
}

public sealed class IdentityAccessDeniedException : Exception
{
    public IdentityAccessDeniedException() : base("Identity access is suspended.") { }
}
