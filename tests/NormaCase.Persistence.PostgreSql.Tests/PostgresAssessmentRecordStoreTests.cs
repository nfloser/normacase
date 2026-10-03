using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresAssessmentRecordStoreTests
{
    [Fact]
    public async Task Migration_is_repeatable_and_records_one_checksum()
    {
        await using var dataSource = CreateDataSource();
        var runner = new PostgresMigrationRunner(dataSource);

        await runner.MigrateAsync();
        await runner.MigrateAsync();

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM normacase.schema_migrations
            WHERE version = 1;
            """,
            connection);

        Assert.Equal(1L, await command.ExecuteScalarAsync());

        await using var auditCommand = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM normacase.schema_migrations
            WHERE version = 2;
            """,
            connection);
        Assert.Equal(1L, await auditCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Append_and_load_roundtrip_preserves_the_strict_record()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = CreateRecord(
            "roundtrip",
            15.1234567890123456789012345m);

        await store.AppendAsync(record);
        var loaded = await store.LoadAsync(record.AssessmentId);

        Assert.NotNull(loaded);
        Assert.Equal(
            AssessmentRecordJson.Serialize(record),
            AssessmentRecordJson.Serialize(loaded!));
        Assert.Equal(
            CaseValue.FromNumber(
                15.1234567890123456789012345m),
            loaded!.Input.Facts["measurement"]);
        Assert.Equal(
            EvidenceStatus.Present,
            loaded.Input.Evidence["verification"]);
        Assert.NotNull(loaded.Result.RuleTrace);
        Assert.Equal(
            record.Result.RuleTrace!.Source,
            loaded.Result.RuleTrace!.Source);
    }

    [Fact]
    public async Task Concurrent_duplicate_identity_cannot_replace_history()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var assessmentId = NewId("duplicate");
        var first = CreateRecord(
            "first",
            15m,
            assessmentId);
        var second = CreateRecord(
            "second",
            99m,
            assessmentId);

        var attempts = await Task.WhenAll(
            Capture(() => store.AppendAsync(first)),
            Capture(() => store.AppendAsync(second)));

        Assert.Equal(
            1,
            attempts.Count(exception => exception is null));
        Assert.Equal(
            1,
            attempts.Count(
                exception => exception
                    is AssessmentRecordAlreadyExistsException));

        var loaded = await store.LoadAsync(assessmentId);
        Assert.NotNull(loaded);
        Assert.Contains(
            loaded!.CaseId,
            new[] { first.CaseId, second.CaseId });
        Assert.Equal(
            loaded.CaseId == first.CaseId
                ? first.Input.Facts["measurement"]
                : second.Input.Facts["measurement"],
            loaded.Input.Facts["measurement"]);
    }

    [Fact]
    public async Task Database_rejects_direct_update_and_delete()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = CreateRecord("append-only", 15m);
        await store.AppendAsync(record);

        await using var connection = await dataSource.OpenConnectionAsync();

        await using var update = new NpgsqlCommand(
            """
            UPDATE normacase.assessment_records
            SET case_id = 'changed'
            WHERE assessment_id = $1;
            """,
            connection);
        update.Parameters.AddWithValue(record.AssessmentId.Value);
        var updateError = await Assert.ThrowsAsync<PostgresException>(
            () => update.ExecuteNonQueryAsync());
        Assert.Equal("55000", updateError.SqlState);

        await using var delete = new NpgsqlCommand(
            """
            DELETE FROM normacase.assessment_records
            WHERE assessment_id = $1;
            """,
            connection);
        delete.Parameters.AddWithValue(record.AssessmentId.Value);
        var deleteError = await Assert.ThrowsAsync<PostgresException>(
            () => delete.ExecuteNonQueryAsync());
        Assert.Equal("55000", deleteError.SqlState);

        Assert.NotNull(await store.LoadAsync(record.AssessmentId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_fingerprint_or_case_metadata_is_rejected(bool mismatchCase)
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var store = new PostgresAssessmentRecordStore(dataSource);
        var record = CreateRecord("bad-hash", 15m);
        var json = AssessmentRecordJson.Serialize(record);

        await using (var connection =
            await dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO normacase.assessment_records (
                assessment_id,
                case_id,
                knowledge_pack_id,
                knowledge_release,
                platform_version,
                assessment_date,
                recorded_at_utc,
                recorded_at_utc_ticks,
                record_format_version,
                record_json,
                record_sha256
            )
            VALUES (
                $1, $2, $3, $4, $5,
                $6, $7, $8, $9, $10, $11
            );
            """,
            connection))
        {
            command.Parameters.AddWithValue(
                record.AssessmentId.Value);
            command.Parameters.AddWithValue(mismatchCase ? "different-case" : record.CaseId.Value);
            command.Parameters.AddWithValue(
                record.KnowledgePackId);
            command.Parameters.AddWithValue(
                record.Result.KnowledgeRelease);
            command.Parameters.AddWithValue(
                record.PlatformVersion);
            command.Parameters.AddWithValue(
                NpgsqlDbType.Date,
                record.Result.AssessmentDate);
            command.Parameters.AddWithValue(
                NpgsqlDbType.TimestampTz,
                record.RecordedAtUtc.UtcDateTime);
            command.Parameters.AddWithValue(
                record.RecordedAtUtc.Ticks);
            command.Parameters.AddWithValue(
                AssessmentRecordJson.CurrentFormatVersion);
            command.Parameters.AddWithValue(
                NpgsqlDbType.Json,
                json);
            command.Parameters.AddWithValue(
                mismatchCase
                    ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()
                    : new string('0', 64));
            await command.ExecuteNonQueryAsync();
        }

        var exception =
            await Assert.ThrowsAsync<AssessmentRecordIntegrityException>(
                () => store.LoadAsync(record.AssessmentId));

        Assert.DoesNotContain(
            json,
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connection_failure_does_not_echo_credentials()
    {
        const string password = "synthetic-secret-password";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "normacase",
            Username = "synthetic-user",
            Password = password,
            Timeout = 1,
            CommandTimeout = 1,
            Pooling = false
        };
        await using var dataSource =
            NpgsqlDataSource.Create(builder.ConnectionString);
        var store = new PostgresAssessmentRecordStore(dataSource);

        var exception =
            await Assert.ThrowsAsync<AssessmentRecordStorageException>(
                () => store.LoadAsync(
                    new AssessmentId("connection-failure")));

        Assert.DoesNotContain(
            password,
            exception.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            builder.ConnectionString,
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task<Exception?> Capture(
        Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static AssessmentRecord CreateRecord(
        string suffix,
        decimal measurement,
        AssessmentId? assessmentId = null)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-c-pack.json"));
        var id = assessmentId
            ?? NewId(suffix);

        return new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = TruthValue.Yes,
                ["measurement"] =
                    CaseValue.FromNumber(measurement),
                ["alternative_confirmed"] =
                    CaseValue.Unknown
            },
            new DateOnly(2026, 10, 2),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = EvidenceStatus.Present
            },
            new AssessmentExecutionContext(
                id,
                new CaseId("case-" + suffix + "-" + Guid.NewGuid().ToString("N")),
                "postgres-test-platform",
                new DateTimeOffset(
                    2026,
                    10,
                    2,
                    22,
                    0,
                    0,
                    TimeSpan.Zero).AddTicks(7)));
    }

    private static AssessmentId NewId(string prefix)
        => new(
            prefix
            + "-"
            + Guid.NewGuid().ToString("N"));

    private static NpgsqlDataSource CreateDataSource()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "NORMACASE_POSTGRES_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "NORMACASE_POSTGRES_TEST_CONNECTION is required.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }
}
