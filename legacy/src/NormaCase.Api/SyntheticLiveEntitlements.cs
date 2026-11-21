using NormaCase.Application.Authorization;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;

namespace NormaCase.Api;

internal sealed class SyntheticEntitlementSnapshot(IdentityEntitlementState state, bool legacy = false)
{
    internal long Revision => state.Revision;
    internal IReadOnlyList<string> Actions => state.Actions;
    internal IReadOnlyList<string> CaseIds => state.CaseIds;
    internal bool Allows(string caseId, string action)
        => legacy ? action != "BATCH" : state.Actions.Contains(action, StringComparer.Ordinal)
            && state.CaseIds.Contains(caseId, StringComparer.Ordinal);
    internal IReadOnlyCollection<CaseId>? ReadScope()
        => legacy ? null : state.Actions.Contains("READ", StringComparer.Ordinal)
            ? state.CaseIds.Select(id => new CaseId(id)).ToArray() : [];
}

internal sealed class SyntheticLiveEntitlements(
    IConfiguration configuration,
    SyntheticReviewCredential credential,
    IReviewedEntitlementChangeStore store)
{
    private readonly bool persistent = configuration.GetValue<bool>("SyntheticReview:PersistenceEnabled");

    internal async Task ReconcileAsync(CancellationToken token = default)
    {
        if (!persistent) return;
        foreach (var actorId in credential.ConfiguredUsers())
            await store.ReconcileBaselineAsync(credential.ConfiguredEntitlementState(actorId), token);
    }

    internal async Task<T> ExecuteAuthorizedAsync<T>(AuthenticatedReviewActor actor,
        Func<SyntheticEntitlementSnapshot, CancellationToken, Task<T>> action,
        CancellationToken token = default)
    {
        EnsureLocal(actor);
        if (!persistent)
            return await action(credential.ConfiguredEntitlementSnapshot(actor.ActorId), token);
        return await store.ExecuteWithEffectiveLockAsync(actor.ActorId,
            (state, lockedToken) => action(new(state), lockedToken), token);
    }

    private static void EnsureLocal(AuthenticatedReviewActor actor)
    {
        if (actor.AuthenticationAuthority != "synthetic-local")
            throw new InvalidOperationException("Verified synthetic identity required.");
    }
}
