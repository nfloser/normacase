using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresWorkflowRunStoreTests
{
    [Fact]
    public async Task Migration_roundtrip_and_historical_reads_preserve_exact_history()
    {
        await using var dataSource = DataSource();
        var migrations = new PostgresMigrationRunner(dataSource);
        await migrations.MigrateAsync();
        await migrations.MigrateAsync();
        var store = new PostgresWorkflowRunStore(dataSource);
        var initial = CreateRun();
        var review = Advance(initial);
        var complete = new WorkflowRunService().Apply(review, 1, "finish", "reviewer",
            Time.AddMinutes(2), "Synthetisch abgeschlossen");

        await store.AppendAsync(initial);
        await store.AppendAsync(review);
        await store.AppendAsync(complete);

        var loaded = await store.LoadLatestAsync(initial.RunId);
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowRunRecordJson.Serialize(complete), WorkflowRunRecordJson.Serialize(loaded!));
        var historical = await store.LoadRevisionAsync(initial.RunId, 0);
        Assert.NotNull(historical);
        Assert.Equal(WorkflowRunRecordJson.Serialize(initial), WorkflowRunRecordJson.Serialize(historical!));
        Assert.Equal(WorkflowRunRecordJson.Serialize(review), WorkflowRunRecordJson.Serialize(Advance(historical!)));
        Assert.Null(await store.LoadRevisionAsync(initial.RunId, 99));
        Assert.Null(await store.LoadLatestAsync(new WorkflowRunId("missing-" + Guid.NewGuid().ToString("N"))));
        Assert.Equal(7, loaded!.History[0].RecordedAtUtc.Ticks % 10);

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM normacase.schema_migrations WHERE version = 3", connection);
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Competing_creation_and_transition_writes_accept_exactly_one_history()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresWorkflowRunStore(dataSource);
        var initial = CreateRun();
        var creation = await Task.WhenAll(Capture(() => store.AppendAsync(initial)), Capture(() => store.AppendAsync(initial)));
        Assert.Single(creation, item => item is null);
        Assert.Single(creation, item => item is WorkflowRunStoreConflictException);

        var first = Advance(initial, "Erste synthetische Prüfung");
        var second = Advance(initial, "Zweite synthetische Prüfung");
        var competing = await Task.WhenAll(Capture(() => store.AppendAsync(first)), Capture(() => store.AppendAsync(second)));
        Assert.Single(competing, item => item is null);
        Assert.Single(competing, item => item is WorkflowRunStoreConflictException);
        var loaded = await store.LoadLatestAsync(initial.RunId);
        Assert.Equal(1, loaded!.Current.Revision);
        Assert.Contains(WorkflowRunRecordJson.Serialize(loaded),
            new[] { WorkflowRunRecordJson.Serialize(first), WorkflowRunRecordJson.Serialize(second) });
    }

    [Fact]
    public async Task Skipped_divergent_and_case_substituted_appends_fail_closed()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresWorkflowRunStore(dataSource);
        var initial = CreateRun();
        var review = Advance(initial);
        await Assert.ThrowsAsync<WorkflowRunStoreConflictException>(() => store.AppendAsync(review));
        await store.AppendAsync(initial);
        var complete = new WorkflowRunService().Apply(review, 1, "finish", "reviewer", Time.AddMinutes(2), "reason");
        await Assert.ThrowsAsync<WorkflowRunStoreConflictException>(() => store.AppendAsync(complete));

        var differentCase = new WorkflowRunRecord(review.RunId, new CaseId("different-case"), review.PlatformVersion, review.History);
        await Assert.ThrowsAsync<WorkflowRunStoreConflictException>(() => store.AppendAsync(differentCase));
        var differentPlatform = new WorkflowRunRecord(review.RunId, review.CaseId, "different-platform", review.History);
        await Assert.ThrowsAsync<WorkflowRunStoreConflictException>(() => store.AppendAsync(differentPlatform));
        var changedCreation = new WorkflowRunEvent(null, "different-creator", Time, "reason", initial.Current);
        var divergent = new WorkflowRunRecord(review.RunId, review.CaseId, review.PlatformVersion, [changedCreation, review.History[1]]);
        await Assert.ThrowsAsync<WorkflowRunStoreConflictException>(() => store.AppendAsync(divergent));

        var loaded = await store.LoadLatestAsync(initial.RunId);
        Assert.Equal(WorkflowRunRecordJson.Serialize(initial), WorkflowRunRecordJson.Serialize(loaded!));
    }

    [Fact]
    public async Task Database_rejects_update_and_delete()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresWorkflowRunStore(dataSource);
        var initial = CreateRun();
        await store.AppendAsync(initial);
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var sql in new[]
        {
            "UPDATE normacase.workflow_run_versions SET case_id = 'changed' WHERE run_id = $1",
            "DELETE FROM normacase.workflow_run_versions WHERE run_id = $1"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(initial.RunId.Value);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }
        Assert.NotNull(await store.LoadLatestAsync(initial.RunId));
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("case")]
    [InlineData("revision")]
    [InlineData("state")]
    [InlineData("platform")]
    [InlineData("knowledge")]
    [InlineData("workflow")]
    [InlineData("ticks")]
    [InlineData("json")]
    public async Task Corrupt_fingerprint_json_or_metadata_is_rejected(string corruption)
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresWorkflowRunStore(dataSource);
        var run = CreateRun();
        var json = corruption == "json" ? "{\"invalid\":true}" : WorkflowRunRecordJson.Serialize(run);
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand("""
            INSERT INTO normacase.workflow_run_versions (
                run_id, revision, case_id, knowledge_pack_id, knowledge_release,
                workflow_id, workflow_version, state_id, platform_version,
                recorded_at_utc, recorded_at_utc_ticks, run_format_version, run_json, run_sha256)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)
            """, connection))
        {
            command.Parameters.AddWithValue(run.RunId.Value);
            command.Parameters.AddWithValue(corruption == "revision" ? 1L : run.Current.Revision);
            command.Parameters.AddWithValue(corruption == "case" ? "substituted" : run.CaseId.Value);
            command.Parameters.AddWithValue(run.Current.KnowledgePackId);
            command.Parameters.AddWithValue(corruption == "knowledge" ? "substituted" : run.Current.KnowledgeRelease);
            command.Parameters.AddWithValue(corruption == "workflow" ? "substituted" : run.Current.WorkflowId);
            command.Parameters.AddWithValue(run.Current.WorkflowVersion);
            command.Parameters.AddWithValue(corruption == "state" ? "substituted" : run.Current.StateId);
            command.Parameters.AddWithValue(corruption == "platform" ? "substituted" : run.PlatformVersion);
            command.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, run.History[^1].RecordedAtUtc.UtcDateTime);
            command.Parameters.AddWithValue(run.History[^1].RecordedAtUtc.Ticks + (corruption == "ticks" ? 1 : 0));
            command.Parameters.AddWithValue(WorkflowRunRecordJson.CurrentFormatVersion);
            command.Parameters.AddWithValue(NpgsqlDbType.Json, json);
            command.Parameters.AddWithValue(corruption == "checksum" ? new string('0', 64)
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
            await command.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<WorkflowRunStoreIntegrityException>(() => store.LoadLatestAsync(run.RunId));
        Assert.DoesNotContain(json, error.ToString(), StringComparison.Ordinal);
        await Assert.ThrowsAsync<WorkflowRunStoreIntegrityException>(() => store.AppendAsync(Advance(run)));
    }

    [Fact]
    public async Task Invalid_queries_and_connection_failures_do_not_echo_credentials()
    {
        const string password = "synthetic-workflow-secret";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 1, Database = "normacase", Username = "synthetic",
            Password = password, Timeout = 1, CommandTimeout = 1, Pooling = false
        };
        await using var source = NpgsqlDataSource.Create(builder.ConnectionString);
        var store = new PostgresWorkflowRunStore(source);
        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadLatestAsync(default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.LoadRevisionAsync(new WorkflowRunId("run"), -1));
        var error = await Assert.ThrowsAsync<WorkflowRunStoreStorageException>(() => store.LoadLatestAsync(new WorkflowRunId("run")));
        Assert.DoesNotContain(password, error.ToString(), StringComparison.Ordinal);
        error = await Assert.ThrowsAsync<WorkflowRunStoreStorageException>(() => store.AppendAsync(CreateRun()));
        Assert.DoesNotContain(password, error.ToString(), StringComparison.Ordinal);
    }

    private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero).AddTicks(7);

    private static WorkflowRunRecord CreateRun()
    {
        var execution = new WorkflowExecutionService().Restore(new(
            "synthetic.pack", "synthetic.release",
            new WorkflowSourceSnapshot("source", "synthetic", "Synthetic source", "SYNTHETIC", "ACTIVE", "1", null, null, null, null, null, null),
            "synthetic.flow", 1, "submitted",
            [new("submitted", false), new("reviewing", false), new("done", true)],
            [new("review", "submitted", "reviewing"), new("finish", "reviewing", "done")], "submitted", 0));
        return new WorkflowRunService().Start(execution, new WorkflowRunId("run-" + Guid.NewGuid().ToString("N")),
            new CaseId("case-synthetic"), "platform-test", "creator", Time, "Synthetischer Start");
    }

    private static WorkflowRunRecord Advance(WorkflowRunRecord run, string reason = "Synthetische Prüfung")
        => new WorkflowRunService().Apply(run, 0, "review", "reviewer", Time.AddMinutes(1), reason);

    private static NpgsqlDataSource DataSource()
        => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("NORMACASE_POSTGRES_TEST_CONNECTION is required."));

    private static async Task<Exception?> Capture(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception exception) { return exception; }
    }
}
