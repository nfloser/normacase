using NormaCase.Application.Authorization;

namespace NormaCase.Api;

internal sealed class SyntheticIdentityAccessGate(
    IIdentityAccessAdministrationStore? store)
{
    internal bool Persistent => store is not null;

    internal async Task<bool> IsActiveAsync(
        string actorId,
        CancellationToken cancellationToken = default)
        => store is null || !(await store.LoadAsync(actorId, cancellationToken)).Suspended;

    internal Task<IdentityAccessState> LoadAsync(
        string actorId,
        CancellationToken cancellationToken = default)
        => Required().LoadAsync(actorId, cancellationToken);

    internal Task<IdentityAccessState> ChangeAsync(
        IdentityAccessChange change,
        CancellationToken cancellationToken = default)
        => Required().ChangeAsync(change, cancellationToken);

    internal Task<IReadOnlyList<IdentityAccessAuditEntry>> LoadHistoryAsync(
        string actorId,
        CancellationToken cancellationToken = default)
        => Required().LoadHistoryAsync(actorId, 100, cancellationToken);

    private IIdentityAccessAdministrationStore Required()
        => store ?? throw new InvalidOperationException(
            "Identity administration requires persistent synthetic review.");
}
