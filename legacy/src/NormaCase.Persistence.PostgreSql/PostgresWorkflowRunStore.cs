using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Workflows;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresWorkflowRunStore : IWorkflowRunStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkflowRunStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async Task AppendAsync(WorkflowRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        var json = WorkflowRunRecordJson.Serialize(run);
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            // A transaction lock also serializes competing creation when no row exists yet.
            // A rare hash collision only serializes unrelated runs; it never joins identities.
            await using (var gate = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1, 105));", connection, transaction))
            {
                gate.Parameters.AddWithValue(run.RunId.Value);
                await gate.ExecuteNonQueryAsync(cancellationToken);
            }

            var stored = await ReadAsync(connection, transaction, run.RunId, null, cancellationToken);
            if (stored is null)
            {
                if (run.Current.Revision != 0 || run.History.Count != 1)
                    throw new WorkflowRunStoreConflictException(run.RunId);
            }
            else
            {
                var current = Verify(run.RunId, stored);
                if (run.Current.Revision != checked(current.Current.Revision + 1)
                    || run.History.Count != current.History.Count + 1)
                    throw new WorkflowRunStoreConflictException(run.RunId);

                var prefix = new WorkflowRunRecord(run.RunId, run.CaseId, run.PlatformVersion,
                    run.History.Take(current.History.Count));
                if (!string.Equals(WorkflowRunRecordJson.Serialize(prefix), stored.Json, StringComparison.Ordinal))
                    throw new WorkflowRunStoreConflictException(run.RunId);
            }

            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.workflow_run_versions (
                    run_id, revision, case_id, knowledge_pack_id, knowledge_release,
                    workflow_id, workflow_version, state_id, platform_version,
                    recorded_at_utc, recorded_at_utc_ticks, run_format_version, run_json, run_sha256)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14);
                """, connection, transaction);
            insert.Parameters.AddWithValue(run.RunId.Value);
            insert.Parameters.AddWithValue(run.Current.Revision);
            insert.Parameters.AddWithValue(run.CaseId.Value);
            insert.Parameters.AddWithValue(run.Current.KnowledgePackId);
            insert.Parameters.AddWithValue(run.Current.KnowledgeRelease);
            insert.Parameters.AddWithValue(run.Current.WorkflowId);
            insert.Parameters.AddWithValue(run.Current.WorkflowVersion);
            insert.Parameters.AddWithValue(run.Current.StateId);
            insert.Parameters.AddWithValue(run.PlatformVersion);
            insert.Parameters.AddWithValue(NpgsqlDbType.TimestampTz, run.History[^1].RecordedAtUtc.UtcDateTime);
            insert.Parameters.AddWithValue(run.History[^1].RecordedAtUtc.Ticks);
            insert.Parameters.AddWithValue(WorkflowRunRecordJson.CurrentFormatVersion);
            insert.Parameters.AddWithValue(NpgsqlDbType.Json, json);
            insert.Parameters.AddWithValue(Sha256(json));
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new WorkflowRunStoreConflictException(run.RunId);
        }
        catch (NpgsqlException)
        {
            throw new WorkflowRunStoreStorageException();
        }
    }

    public Task<WorkflowRunRecord?> LoadLatestAsync(WorkflowRunId runId, CancellationToken cancellationToken = default)
        => LoadAsync(runId, null, cancellationToken);

    public Task<WorkflowRunRecord?> LoadRevisionAsync(WorkflowRunId runId, long revision, CancellationToken cancellationToken = default)
    {
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        return LoadAsync(runId, revision, cancellationToken);
    }

    private async Task<WorkflowRunRecord?> LoadAsync(WorkflowRunId runId, long? revision, CancellationToken cancellationToken)
    {
        if (runId.IsEmpty)
            throw new ArgumentException("Workflow run id must be explicit.", nameof(runId));
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var stored = await ReadAsync(connection, null, runId, revision, cancellationToken);
            return stored is null ? null : Verify(runId, stored);
        }
        catch (NpgsqlException)
        {
            throw new WorkflowRunStoreStorageException();
        }
    }

    private static async Task<StoredRun?> ReadAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, WorkflowRunId runId,
        long? revision, CancellationToken cancellationToken)
    {
        var sql = """
            SELECT revision, case_id, knowledge_pack_id, knowledge_release,
                   workflow_id, workflow_version, state_id, platform_version,
                   recorded_at_utc, recorded_at_utc_ticks, run_format_version,
                   run_json::text, run_sha256
            FROM normacase.workflow_run_versions
            WHERE run_id = $1
            """ + (revision is null ? " ORDER BY revision DESC LIMIT 1;" : " AND revision = $2;");
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(runId.Value);
        if (revision is not null)
            command.Parameters.AddWithValue(revision.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt32(5), reader.GetString(6), reader.GetString(7),
            new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)),
            reader.GetInt64(9), reader.GetInt32(10), reader.GetString(11), reader.GetString(12));
    }

    private static WorkflowRunRecord Verify(WorkflowRunId runId, StoredRun stored)
    {
        if (stored.FormatVersion != WorkflowRunRecordJson.CurrentFormatVersion
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored.Checksum), Encoding.ASCII.GetBytes(Sha256(stored.Json))))
            throw new WorkflowRunStoreIntegrityException(runId);

        WorkflowRunRecord run;
        try { run = WorkflowRunRecordJson.Deserialize(stored.Json); }
        catch (JsonException) { throw new WorkflowRunStoreIntegrityException(runId); }

        var time = run.History[^1].RecordedAtUtc;
        var truncated = new DateTimeOffset(time.Ticks - time.Ticks % 10, TimeSpan.Zero);
        if (run.RunId != runId
            || run.Current.Revision != stored.Revision
            || run.CaseId.Value != stored.CaseId
            || run.Current.KnowledgePackId != stored.KnowledgePackId
            || run.Current.KnowledgeRelease != stored.KnowledgeRelease
            || run.Current.WorkflowId != stored.WorkflowId
            || run.Current.WorkflowVersion != stored.WorkflowVersion
            || run.Current.StateId != stored.StateId
            || run.PlatformVersion != stored.PlatformVersion
            || time.Ticks != stored.RecordedAtUtcTicks
            || truncated != stored.RecordedAtUtc)
            throw new WorkflowRunStoreIntegrityException(runId);
        return run;
    }

    private static string Sha256(string json)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    private sealed record StoredRun(
        long Revision, string CaseId, string KnowledgePackId, string KnowledgeRelease,
        string WorkflowId, int WorkflowVersion, string StateId, string PlatformVersion,
        DateTimeOffset RecordedAtUtc, long RecordedAtUtcTicks, int FormatVersion,
        string Json, string Checksum);
}
