using Npgsql;
using NormaCase.Persistence.PostgreSql;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresBatchReviewRequestStoreTests
{
    [Fact]
    public async Task Exact_result_and_server_time_survive_restart_while_changed_reuse_fails()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var id = "batch-" + Guid.NewGuid().ToString("N");
        var timestamp = new DateTimeOffset(2026, 10, 4, 7, 30, 0, TimeSpan.Zero);
        var registration = new BatchReviewRequestRegistration(id, "synthetic-local:user-a",
            new string('a', 64), timestamp);
        await using (var lease = await new PostgresBatchReviewRequestStore(source).AcquireAsync(registration))
        {
            Assert.Null(lease.ResultJson);
            Assert.Equal(timestamp, lease.RecordedAtUtc);
            await lease.CommitResultAsync("{\"status\":\"COMMITTED\"}");
        }

        await using (var replay = await new PostgresBatchReviewRequestStore(source).AcquireAsync(
            registration with { ProposedRecordedAtUtc = timestamp.AddHours(1) }))
        {
            Assert.Equal(timestamp, replay.RecordedAtUtc);
            Assert.Equal("{\"status\":\"COMMITTED\"}", replay.ResultJson);
        }

        await Assert.ThrowsAsync<BatchReviewRequestConflictException>(async () =>
        {
            await using var _ = await new PostgresBatchReviewRequestStore(source).AcquireAsync(
                registration with { RequestSha256 = new string('b', 64) });
        });
        await Assert.ThrowsAsync<BatchReviewRequestConflictException>(async () =>
        {
            await using var _ = await new PostgresBatchReviewRequestStore(source).AcquireAsync(
                registration with { ActorId = "synthetic-local:user-b" });
        });
    }

    [Fact]
    public async Task Concurrent_equal_request_waits_for_and_reads_one_append_only_result()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var registration = new BatchReviewRequestRegistration(
            "concurrent-" + Guid.NewGuid().ToString("N"),
            "synthetic-local:user-a", new string('c', 64),
            new DateTimeOffset(2026, 10, 4, 7, 31, 0, TimeSpan.Zero));
        await using var first = await new PostgresBatchReviewRequestStore(source).AcquireAsync(registration);
        var waiting = new PostgresBatchReviewRequestStore(source).AcquireAsync(registration);
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);
        await first.CommitResultAsync("{\"items\":[]}");
        await first.DisposeAsync();
        await using var second = await waiting;
        Assert.Equal("{\"items\":[]}", second.ResultJson);
    }

    [Fact]
    public async Task Database_rejects_request_and_result_mutation()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var id = "immutable-" + Guid.NewGuid().ToString("N");
        await using (var lease = await new PostgresBatchReviewRequestStore(source).AcquireAsync(
            new(id, "synthetic-local:user-a", new string('d', 64),
                new DateTimeOffset(2026, 10, 4, 7, 32, 0, TimeSpan.Zero))))
            await lease.CommitResultAsync("{\"items\":[]}");

        foreach (var sql in new[]
        {
            "UPDATE normacase.batch_review_requests SET actor_id=actor_id WHERE request_id=$1",
            "DELETE FROM normacase.batch_review_requests WHERE request_id=$1",
            "UPDATE normacase.batch_review_results SET result_json=result_json WHERE request_id=$1",
            "DELETE FROM normacase.batch_review_results WHERE request_id=$1"
        })
        {
            await using var command = source.CreateCommand(sql);
            command.Parameters.AddWithValue(id);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }
    }

    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(
        Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Synthetic PostgreSQL test configuration required."));
}
