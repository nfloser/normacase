using Npgsql;
using NormaCase.Application.Authorization;
using NormaCase.Persistence.PostgreSql;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresIdentityAccessAdministrationStoreTests
{
    [Fact]
    public async Task Changes_are_revision_checked_persistent_and_append_only()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:user-" + Guid.NewGuid().ToString("N");
        var administrator = "synthetic-local:administrator";
        var store = new PostgresIdentityAccessAdministrationStore(source);

        Assert.Equal(new IdentityAccessState(actor, 0, false), await store.LoadAsync(actor));
        var suspended = await store.ChangeAsync(new(
            actor, 0, true, administrator,
            new DateTimeOffset(2026, 10, 3, 20, 0, 0, TimeSpan.Zero),
            "Synthetische Sperrprüfung"));
        Assert.Equal(new IdentityAccessState(actor, 1, true), suspended);
        await Assert.ThrowsAsync<IdentityAccessConflictException>(() => store.ChangeAsync(new(
            actor, 0, false, administrator,
            new DateTimeOffset(2026, 10, 3, 20, 1, 0, TimeSpan.Zero),
            "Veraltete synthetische Änderung")));

        var restarted = new PostgresIdentityAccessAdministrationStore(source);
        Assert.Equal(suspended, await restarted.LoadAsync(actor));
        var active = await restarted.ChangeAsync(new(
            actor, 1, false, administrator,
            new DateTimeOffset(2026, 10, 3, 20, 2, 0, TimeSpan.Zero),
            "Synthetische Reaktivierung"));
        Assert.Equal(new IdentityAccessState(actor, 2, false), active);
        var history = await restarted.LoadHistoryAsync(actor, 100);
        Assert.Equal(new long[] { 2, 1 }, history.Select(item => item.Revision));
        Assert.All(history, item => Assert.Equal(administrator, item.AdministratorActorId));

        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[]
        {
            "UPDATE normacase.identity_access_audit SET suspended=suspended WHERE actor_id=$1;",
            "DELETE FROM normacase.identity_access_audit WHERE actor_id=$1;"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(actor);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }
    }

    [Fact]
    public async Task Concurrent_initial_changes_allow_exactly_one_revision_one()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var actor = "synthetic-local:user-" + Guid.NewGuid().ToString("N");
        var stores = Enumerable.Range(0, 4)
            .Select(_ => new PostgresIdentityAccessAdministrationStore(source)).ToArray();
        var tasks = stores.Select(store => Capture(store.ChangeAsync(new(
            actor, 0, true, "synthetic-local:administrator",
            new DateTimeOffset(2026, 10, 3, 20, 0, 0, TimeSpan.Zero),
            "Gleichzeitige synthetische Sperre")))).ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Single(results, result => result.State is not null);
        Assert.Equal(3, results.Count(result => result.Error is IdentityAccessConflictException));
    }

    private static async Task<(IdentityAccessState? State, Exception? Error)> Capture(
        Task<IdentityAccessState> task)
    {
        try { return (await task, null); }
        catch (Exception exception) { return (null, exception); }
    }

    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(
        Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic PostgreSQL test configuration required."));
}
