using Npgsql;
using NormaCase.Application.Intake;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;
namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresIntakeStoreTests
{
    [Fact]
    public async Task Durable_intake_returns_original_alias_receipts_and_rejects_conflicts()
    {
        await using var source = Source(); await new PostgresMigrationRunner(source).MigrateAsync();
        var prefix = "intake-" + Guid.NewGuid().ToString("N");
        var store = new PostgresNormalizedIntakeStore(source);
        var original = Record(prefix, "message-one", 1);
        Assert.Equal(IntakeAcceptance.Accepted, (await store.AppendAsync(original)).Acceptance);
        var alias = Record(prefix, "message-two", 1, timeOffset: 1);
        var duplicate = await store.AppendAsync(alias);
        Assert.Equal(NormalizedIntakeJson.Serialize(original), NormalizedIntakeJson.Serialize(duplicate.Record));
        Assert.Equal(IntakeAcceptance.Duplicate, duplicate.Acceptance);
        await Assert.ThrowsAsync<IntakeConflictException>(() => store.AppendAsync(Record(prefix, "message-two", 2)));
        await store.AppendAsync(Record(prefix, "message-three", 3));
        await Assert.ThrowsAsync<IntakeConflictException>(() => store.AppendAsync(Record(prefix, "message-stale", 2)));
        await Assert.ThrowsAsync<IntakeConflictException>(() => store.AppendAsync(Record(prefix, "message-changed", 1, TruthValue.No)));
        await Assert.ThrowsAsync<IntakeConflictException>(() => store.AppendAsync(Record(prefix, "message-other", 4, caseId: prefix + "-other")));
        var reloaded = await new PostgresNormalizedIntakeStore(source).LoadAsync(prefix, "upstream", 1);
        Assert.Equal(NormalizedIntakeJson.Serialize(original), NormalizedIntakeJson.Serialize(reloaded!));
        Assert.Equal(IntakeAcceptance.Duplicate, (await store.AppendAsync(original)).Acceptance);
        await Assert.ThrowsAsync<IntakeConflictException>(() => store.AppendAsync(Record(prefix + "-other-source", "message-one", 1, caseId: prefix)));
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "intake_streams", "intake_records", "intake_messages" })
        {
            foreach (var sql in new[] { $"DELETE FROM normacase.{table} WHERE source_system_id=$1", $"UPDATE normacase.{table} SET source_system_id=source_system_id WHERE source_system_id=$1" })
            {
                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue(prefix);
                Assert.Equal("55000", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
            }
        }
    }
    [Fact]
    public async Task Concurrent_identical_deliveries_have_one_original_receipt()
    {
        await using var source = Source(); await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresNormalizedIntakeStore(source); var original = Record("concurrent-" + Guid.NewGuid().ToString("N"), "message", 1);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => store.AppendAsync(original)));
        Assert.Single(receipts, item => item.Acceptance == IntakeAcceptance.Accepted);
        Assert.All(receipts, item => Assert.Equal(NormalizedIntakeJson.Serialize(original), NormalizedIntakeJson.Serialize(item.Record)));
    }
    [Fact]
    public async Task Cancelled_append_does_not_reserve_a_message_or_record()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresNormalizedIntakeStore(source);
        var record = Record("cancelled-" + Guid.NewGuid().ToString("N"), "message", 1);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AppendAsync(record, cancelled.Token));
        Assert.Null(await store.LoadAsync(record.Provenance.SourceSystemId, "upstream", 1));
        Assert.Equal(IntakeAcceptance.Accepted, (await store.AppendAsync(record)).Acceptance);
    }
    private static NormalizedIntakeRecord Record(string source, string message, long revision, TruthValue value = TruthValue.Yes, string? caseId = null, int timeOffset = 0)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));
        return new NormalizedIntakeService(new Unused()).Normalize(new(new(caseId ?? source), "synthetic-type", new(source, "upstream", message, revision, "synthetic-adapter", 1, new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero).AddMinutes(timeOffset)), new(2026, 10, 3), new Dictionary<string, CaseValue> { { "request_confirmed", value }, { "measurement", 15.1234567890123456789m }, { "alternative_confirmed", TruthValue.No } }, new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(), new Dictionary<string, IReadOnlyList<string>>()), pack);
    }
    private sealed class Unused : INormalizedIntakeStore { public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken token = default) => throw new NotSupportedException(); }
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION") ?? throw new InvalidOperationException("Synthetic PostgreSQL test configuration required."));
}
