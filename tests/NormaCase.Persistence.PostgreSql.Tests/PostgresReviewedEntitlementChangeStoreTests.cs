using Npgsql;
using NormaCase.Application.Authorization;
using NormaCase.Persistence.PostgreSql;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresReviewedEntitlementChangeStoreTests
{
    [Fact]
    public async Task Distinct_approval_activates_an_exact_append_only_snapshot_across_restart()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:user-" + Guid.NewGuid().ToString("N");
        var changeId = "entitlement-" + Guid.NewGuid().ToString("N");
        var store = new PostgresReviewedEntitlementChangeStore(source);
        await store.ReconcileBaselineAsync(new(actor, 0, ["READ"], ["demo-g-not-supported"]));
        var proposal = Proposal(changeId, actor, 0, "synthetic-local:access-proposer", "demo-g-supported");

        var baseline = await store.LoadEffectiveAsync(actor);
        Assert.Equal(0, baseline.Revision);
        Assert.Equal(new[] { "demo-g-not-supported" }, baseline.CaseIds);
        await store.ProposeAsync(proposal);
        var decided = await store.DecideAsync(new(
            changeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), true,
            "Synthetische Gegenprüfung"), true);
        Assert.True(decided.Decision!.Approved);

        var restarted = new PostgresReviewedEntitlementChangeStore(source);
        var effective = await restarted.LoadEffectiveAsync(actor);
        Assert.Equal(1, effective.Revision);
        Assert.Equal(new[] { "ACCEPT", "READ" }, effective.Actions);
        Assert.Equal(new[] { "demo-g-supported" }, effective.CaseIds);
        Assert.NotNull((await restarted.LoadChangeAsync(changeId))!.Decision);

        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[]
        {
            "UPDATE normacase.identity_entitlement_changes SET reason=reason WHERE change_id=$1",
            "DELETE FROM normacase.identity_entitlement_decisions WHERE change_id=$1",
            "UPDATE normacase.identity_entitlement_versions SET actions=actions WHERE actor_id=$2",
            "UPDATE normacase.identity_entitlement_baselines SET actions=actions WHERE actor_id=$2"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(changeId);
            command.Parameters.AddWithValue(actor);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }
    }

    [Fact]
    public async Task Rejection_self_approval_stale_revision_and_competing_decisions_fail_closed()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:user-" + Guid.NewGuid().ToString("N");
        var store = new PostgresReviewedEntitlementChangeStore(source);
        await store.ReconcileBaselineAsync(new(actor, 0, [], []));

        var rejected = Proposal("entitlement-" + Guid.NewGuid().ToString("N"), actor, 0,
            "synthetic-local:access-proposer", "demo-g-not-supported");
        await store.ProposeAsync(rejected);
        Assert.Equal(rejected.ChangeId, (await store.ListPendingAsync(100)).Single().Proposal.ChangeId);
        await Assert.ThrowsAsync<EntitlementSeparationOfDutiesException>(() => store.DecideAsync(new(
            rejected.ChangeId, rejected.ProposerActorId,
            new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), true, "Unzulässige Selbstfreigabe"), true));
        await store.DecideAsync(new(rejected.ChangeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 2, 0, TimeSpan.Zero), false, "Synthetisch abgelehnt"), true);
        Assert.Empty(await store.ListPendingAsync(100));
        Assert.Equal(0, (await store.LoadEffectiveAsync(actor)).Revision);

        var winner = Proposal("entitlement-" + Guid.NewGuid().ToString("N"), actor, 0,
            "synthetic-local:access-proposer", "demo-g-supported");
        var stale = Proposal("entitlement-" + Guid.NewGuid().ToString("N"), actor, 0,
            "synthetic-local:access-proposer", "demo-g-review");
        await store.ProposeAsync(winner); await store.ProposeAsync(stale);
        var decision = new EntitlementChangeDecision(winner.ChangeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 3, 0, TimeSpan.Zero), true, "Synthetisch freigegeben");
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Capture(
            new PostgresReviewedEntitlementChangeStore(source).DecideAsync(decision, true))));
        Assert.Single(results, item => item.Record is not null);
        Assert.Equal(3, results.Count(item => item.Error is EntitlementChangeConflictException));
        await Assert.ThrowsAsync<EntitlementChangeConflictException>(() => store.DecideAsync(new(
            stale.ChangeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 4, 0, TimeSpan.Zero), true, "Veraltete Freigabe"), true));
    }

    [Fact]
    public async Task Baseline_mismatch_fails_and_live_operation_serializes_with_approval()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:user-" + Guid.NewGuid().ToString("N");
        var store = new PostgresReviewedEntitlementChangeStore(source);
        await store.ReconcileBaselineAsync(new(actor, 0, ["READ"], ["demo-g-supported"]));
        await Assert.ThrowsAsync<EntitlementChangeConflictException>(() =>
            store.ReconcileBaselineAsync(new(actor, 0, ["READ"], ["demo-g-review"])));

        var proposal = Proposal("entitlement-" + Guid.NewGuid().ToString("N"), actor, 0,
            "synthetic-local:access-proposer", "demo-g-review");
        await store.ProposeAsync(proposal);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = store.ExecuteWithEffectiveLockAsync(actor, async (snapshot, _) =>
        {
            Assert.Equal(0, snapshot.Revision);
            entered.SetResult(true);
            await release.Task;
            return snapshot;
        });
        await entered.Task;
        var approval = store.DecideAsync(new(proposal.ChangeId, "synthetic-local:access-approver",
            new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), true, "Synthetische Gegenprüfung"), true);
        Assert.False(approval.IsCompleted);
        release.SetResult(true);
        Assert.Equal(0, (await operation).Revision);
        Assert.Equal(1, (await approval).Proposal.ExpectedEntitlementRevision + 1);
        Assert.Equal(1, (await store.LoadEffectiveAsync(actor)).Revision);
    }

    [Fact]
    public async Task Account_suspension_serializes_with_operation_and_rechecks_after_authentication()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:suspend-" + Guid.NewGuid().ToString("N");
        var store = new PostgresReviewedEntitlementChangeStore(source);
        await store.ReconcileBaselineAsync(new(actor, 0, ["READ"], ["demo-g-supported"]));
        var access = new PostgresIdentityAccessAdministrationStore(source);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = store.ExecuteWithEffectiveLockAsync(actor, async (state, _) =>
        {
            Assert.NotEmpty(state.Actions);
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return true;
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var suspension = access.ChangeAsync(new(actor, 0, true, "synthetic-local:administrator",
            DateTimeOffset.UnixEpoch, "Synthetische Sperre"));
        Assert.False(suspension.IsCompleted);
        release.SetResult();
        Assert.True(await operation);
        Assert.True((await suspension).Suspended);
        await store.ExecuteWithEffectiveLockAsync(actor, (state, _) =>
        {
            Assert.Empty(state.Actions);
            Assert.Empty(state.CaseIds);
            return Task.FromResult(true);
        });
    }

    private static EntitlementChangeProposal Proposal(
        string changeId, string actor, long revision, string proposer, string caseId) => new(
            changeId, actor, revision, ["READ", "ACCEPT"], [caseId], proposer,
            new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), "Synthetischer Rechteantrag");

    private static async Task<(EntitlementChangeRecord? Record, Exception? Error)> Capture(
        Task<EntitlementChangeRecord> task)
    {
        try { return (await task, null); }
        catch (Exception exception) { return (null, exception); }
    }

    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(
        Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic PostgreSQL test configuration required."));
}
