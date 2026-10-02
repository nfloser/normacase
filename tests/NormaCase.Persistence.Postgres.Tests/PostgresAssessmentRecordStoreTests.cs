using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.Postgres;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.Postgres.Tests;

public sealed class PostgresAssessmentRecordStoreTests
{
    private static string ConnectionString
        => Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
           ?? "Host=localhost;Port=5432;Database=normacase;Username=postgres;Password=postgres";

    [Fact]
    public async Task Migration_is_repeatable_and_records_one_applied_version()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        var migrator = new PostgresSchemaMigrator(dataSource);

        await migrator.ApplyAsync();
        await migrator.ApplyAsync();

        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM normacase.schema_migrations WHERE version = 1");
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Stored_record_roundtrips_exact_json_decimal_unknown_and_source_trace()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = Record(
            "postgres-roundtrip-" + Guid.NewGuid().ToString("N"),
            "demo-b",
            new Dictionary<string, CaseValue>
            {
                ["score"] = CaseValue.FromNumber(123456789.1234567890123456789m)
            });

        await store.AppendAsync(record);
        var restored = await store.FindAsync(record.AssessmentId);

        Assert.NotNull(restored);
        Assert.Equal(AssessmentRecordJson.Serialize(record), AssessmentRecordJson.Serialize(restored!));
        Assert.Equal(
            CaseValue.FromNumber(123456789.1234567890123456789m),
            restored!.Input.Facts["score"]);
        Assert.Equal(CaseValue.Unknown, restored.Input.Facts["band_value"]);
        Assert.Equal(record.Result.RuleTrace!.Source, restored.Result.RuleTrace!.Source);

        await using var command = dataSource.CreateCommand(
            "SELECT record_json::text, record_sha256 FROM normacase.assessment_records WHERE assessment_id = $1");
        command.Parameters.AddWithValue(record.AssessmentId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var storedJson = reader.GetString(0);
        Assert.Equal(AssessmentRecordJson.Serialize(record), storedJson);
        Assert.Equal(Sha256(storedJson), reader.GetString(1));
    }

    [Theory]
    [InlineData(EvidenceStatus.Present)]
    [InlineData(EvidenceStatus.Missing)]
    [InlineData(EvidenceStatus.Conflicting)]
    public async Task Evidence_state_survives_storage(EvidenceStatus status)
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = Record(
            "postgres-evidence-" + status + "-" + Guid.NewGuid().ToString("N"),
            "demo-c",
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = TruthValue.Yes,
                ["measurement"] = 15m
            },
            new Dictionary<string, EvidenceStatus> { ["verification"] = status });

        await store.AppendAsync(record);
        var restored = await store.FindAsync(record.AssessmentId);

        Assert.Equal(status, restored!.Input.Evidence["verification"]);
    }

    [Fact]
    public async Task Duplicate_identity_cannot_replace_the_original_record()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var assessmentId = "postgres-duplicate-" + Guid.NewGuid().ToString("N");
        var original = Record(
            assessmentId,
            "demo-b",
            new Dictionary<string, CaseValue> { ["score"] = 30m });
        var replacement = Record(
            assessmentId,
            "demo-b",
            new Dictionary<string, CaseValue> { ["score"] = 99m });

        await store.AppendAsync(original);
        await Assert.ThrowsAsync<AssessmentRecordAlreadyExistsException>(
            () => store.AppendAsync(replacement).AsTask());

        var restored = await store.FindAsync(original.AssessmentId);
        Assert.Equal(
            AssessmentRecordJson.Serialize(original),
            AssessmentRecordJson.Serialize(restored!));
    }

    [Theory]
    [InlineData("UPDATE normacase.assessment_records SET case_id = 'changed' WHERE assessment_id = $1")]
    [InlineData("DELETE FROM normacase.assessment_records WHERE assessment_id = $1")]
    public async Task Database_rejects_mutation_of_assessment_history(string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = Record(
            "postgres-immutable-" + Guid.NewGuid().ToString("N"),
            "demo-b",
            new Dictionary<string, CaseValue> { ["score"] = 30m });
        await store.AppendAsync(record);

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(record.AssessmentId.Value);
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal("55000", exception.SqlState);
        Assert.NotNull(await store.FindAsync(record.AssessmentId));
    }

    [Fact]
    public async Task Database_rejects_truncating_assessment_history()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();

        await using var command = dataSource.CreateCommand(
            "TRUNCATE TABLE normacase.assessment_records");
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal("55000", exception.SqlState);
    }

    [Fact]
    public async Task Corrupted_stored_json_is_rejected_without_leaking_payload()
    {
        await using var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await new PostgresSchemaMigrator(dataSource).ApplyAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = Record(
            "postgres-integrity-" + Guid.NewGuid().ToString("N"),
            "demo-b",
            new Dictionary<string, CaseValue> { ["score"] = 30m });
        await store.AppendAsync(record);

        await using (var disable = dataSource.CreateCommand(
                         "ALTER TABLE normacase.assessment_records DISABLE TRIGGER assessment_records_immutable"))
            await disable.ExecuteNonQueryAsync();
        try
        {
            await using var corrupt = dataSource.CreateCommand(
                "UPDATE normacase.assessment_records SET record_json = $2::json WHERE assessment_id = $1");
            corrupt.Parameters.AddWithValue(record.AssessmentId.Value);
            corrupt.Parameters.AddWithValue("{\"tampered\":true}");
            await corrupt.ExecuteNonQueryAsync();
        }
        finally
        {
            await using var enable = dataSource.CreateCommand(
                "ALTER TABLE normacase.assessment_records ENABLE TRIGGER assessment_records_immutable");
            await enable.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<AssessmentRecordIntegrityException>(
            () => store.FindAsync(record.AssessmentId).AsTask());

        Assert.DoesNotContain("tampered", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(record.CaseId, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connection_failure_is_sanitized()
    {
        const string secret = "postgres-test-secret";
        await using var dataSource = NpgsqlDataSource.Create(
            $"Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password={secret};Timeout=1;Command Timeout=1");
        var store = new PostgresAssessmentRecordStore(dataSource);

        var exception = await Assert.ThrowsAsync<AssessmentRecordStorageException>(
            () => store.FindAsync(new AssessmentId("missing")).AsTask());

        Assert.DoesNotContain(secret, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", exception.ToString(), StringComparison.Ordinal);
    }

    private static AssessmentRecord Record(
        string id,
        string demo,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus>? evidence = null)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", demo + "-pack.json"));
        return new AssessmentRecorder().Evaluate(
            pack,
            facts,
            new DateOnly(2026, 10, 2),
            evidence,
            new AssessmentExecutionContext(
                new AssessmentId(id),
                "synthetic-case-" + demo,
                "postgres-test-platform",
                new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero)));
    }

    private static string Sha256(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
