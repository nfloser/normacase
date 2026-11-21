using System.Text.RegularExpressions;

namespace NormaCase.Application.Authorization;

public sealed class EntitlementChangeProposal
{
    private static readonly Regex IdentityPattern = new(
        "\\A[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex ActionPattern = new(
        "\\A[A-Z][A-Z0-9_]{0,63}\\z", RegexOptions.CultureInvariant);

    public string ChangeId { get; }
    public string TargetActorId { get; }
    public long ExpectedEntitlementRevision { get; }
    public IReadOnlyList<string> Actions { get; }
    public IReadOnlyList<string> CaseIds { get; }
    public string ProposerActorId { get; }
    public DateTimeOffset ProposedAtUtc { get; }
    public string Reason { get; }

    public EntitlementChangeProposal(
        string changeId,
        string targetActorId,
        long expectedEntitlementRevision,
        IEnumerable<string> actions,
        IEnumerable<string> caseIds,
        string proposerActorId,
        DateTimeOffset proposedAtUtc,
        string reason)
    {
        ChangeId = ValidateIdentity(changeId, nameof(changeId));
        TargetActorId = ValidateIdentity(targetActorId, nameof(targetActorId));
        ProposerActorId = ValidateIdentity(proposerActorId, nameof(proposerActorId));
        if (expectedEntitlementRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedEntitlementRevision));
        ExpectedEntitlementRevision = expectedEntitlementRevision;
        ProposedAtUtc = ValidateTime(proposedAtUtc, nameof(proposedAtUtc));
        Reason = ValidateReason(reason, nameof(reason));
        Actions = ValidSet(actions, 32, ActionPattern, nameof(actions));
        CaseIds = ValidSet(caseIds, 500, IdentityPattern, nameof(caseIds));
    }

    public static string ValidateIdentity(string value, string parameter)
    {
        if (value is null || !IdentityPattern.IsMatch(value)) throw new ArgumentException("Invalid identity.", parameter);
        return value;
    }

    public static DateTimeOffset ValidateTime(DateTimeOffset value, string parameter)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("UTC is required.", parameter);
        return value;
    }

    public static string ValidateReason(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1000 || value.Any(char.IsControl))
            throw new ArgumentException("Invalid reason.", parameter);
        return value;
    }

    private static IReadOnlyList<string> ValidSet(
        IEnumerable<string> values, int maximum, Regex pattern, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values);
        var supplied = values.ToArray();
        if (supplied.Length > maximum || supplied.Any(value => value is null || !pattern.IsMatch(value))
            || supplied.Distinct(StringComparer.Ordinal).Count() != supplied.Length)
            throw new ArgumentException("Invalid entitlement set.", parameter);
        return Array.AsReadOnly(supplied.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }
}

public sealed class EntitlementChangeDecision
{
    public string ChangeId { get; }
    public string DecisionActorId { get; }
    public DateTimeOffset DecidedAtUtc { get; }
    public bool Approved { get; }
    public string Reason { get; }

    public EntitlementChangeDecision(
        string changeId, string decisionActorId, DateTimeOffset decidedAtUtc,
        bool approved, string reason)
    {
        ChangeId = EntitlementChangeProposal.ValidateIdentity(changeId, nameof(changeId));
        DecisionActorId = EntitlementChangeProposal.ValidateIdentity(decisionActorId, nameof(decisionActorId));
        DecidedAtUtc = EntitlementChangeProposal.ValidateTime(decidedAtUtc, nameof(decidedAtUtc));
        Approved = approved;
        Reason = EntitlementChangeProposal.ValidateReason(reason, nameof(reason));
    }
}

public sealed record EntitlementAdministrationPolicy(bool RequireDistinctDecisionActor);

public sealed record EntitlementChangeRecord(
    EntitlementChangeProposal Proposal,
    EntitlementChangeDecision? Decision);

public sealed class IdentityEntitlementState
{
    public string ActorId { get; }
    public long Revision { get; }
    public IReadOnlyList<string> Actions { get; }
    public IReadOnlyList<string> CaseIds { get; }

    public IdentityEntitlementState(
        string actorId, long revision, IEnumerable<string> actions, IEnumerable<string> caseIds)
    {
        ActorId = EntitlementChangeProposal.ValidateIdentity(actorId, nameof(actorId));
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        Revision = revision;
        // Reuse proposal validation and its defensive sorted copies.
        var validated = new EntitlementChangeProposal(
            "state-validation", actorId, revision, actions, caseIds,
            "synthetic-state-loader", DateTimeOffset.UnixEpoch, "state validation");
        Actions = validated.Actions;
        CaseIds = validated.CaseIds;
    }
}

public interface IReviewedEntitlementChangeStore
{
    Task ReconcileBaselineAsync(
        IdentityEntitlementState baseline, CancellationToken cancellationToken = default);
    Task<EntitlementChangeRecord> ProposeAsync(
        EntitlementChangeProposal proposal, CancellationToken cancellationToken = default);
    Task<EntitlementChangeRecord> DecideAsync(
        EntitlementChangeDecision decision, bool requireDistinctDecisionActor,
        CancellationToken cancellationToken = default);
    Task<EntitlementChangeRecord?> LoadChangeAsync(
        string changeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EntitlementChangeRecord>> ListPendingAsync(
        int limit, CancellationToken cancellationToken = default);
    Task<IdentityEntitlementState> LoadEffectiveAsync(
        string actorId, CancellationToken cancellationToken = default);
    Task<T> ExecuteWithEffectiveLockAsync<T>(
        string actorId, Func<IdentityEntitlementState, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}

public sealed class ReviewedEntitlementChangeService(
    IReviewedEntitlementChangeStore store,
    EntitlementAdministrationPolicy policy)
{
    public Task<EntitlementChangeRecord> ProposeAsync(
        EntitlementChangeProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return store.ProposeAsync(proposal, cancellationToken);
    }

    public Task<EntitlementChangeRecord> DecideAsync(
        EntitlementChangeDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return DecideValidatedAsync(decision, cancellationToken);
    }

    private async Task<EntitlementChangeRecord> DecideValidatedAsync(
        EntitlementChangeDecision decision, CancellationToken cancellationToken)
    {
        var existing = await store.LoadChangeAsync(decision.ChangeId, cancellationToken)
            ?? throw new EntitlementChangeNotFoundException();
        if (policy.RequireDistinctDecisionActor
            && string.Equals(existing.Proposal.ProposerActorId, decision.DecisionActorId, StringComparison.Ordinal))
            throw new EntitlementSeparationOfDutiesException();
        if (decision.DecidedAtUtc < existing.Proposal.ProposedAtUtc)
            throw new EntitlementChangeConflictException();
        return await store.DecideAsync(
            decision, policy.RequireDistinctDecisionActor, cancellationToken);
    }
}

public sealed class EntitlementChangeConflictException : Exception
{
    public EntitlementChangeConflictException() : base("Entitlement change conflicts with current state.") { }
}

public sealed class EntitlementChangeNotFoundException : Exception
{
    public EntitlementChangeNotFoundException() : base("Entitlement change was not found.") { }
}

public sealed class EntitlementSeparationOfDutiesException : Exception
{
    public EntitlementSeparationOfDutiesException() : base("A distinct decision actor is required.") { }
}

public sealed class EntitlementChangeStorageException : Exception
{
    public EntitlementChangeStorageException() : base("Entitlement change storage operation failed.") { }
}
