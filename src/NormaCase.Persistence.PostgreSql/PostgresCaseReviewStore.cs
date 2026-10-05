using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class CaseReviewStorageException : Exception
{
    public CaseReviewStorageException() : base("Case review storage operation failed.") { }
}
public sealed class CaseReviewIntegrityException : Exception
{
    public CaseReviewIntegrityException() : base("Stored case review history failed verification.") { }
}

public sealed partial class PostgresCaseReviewStore(NpgsqlDataSource dataSource, Func<string, int, WorkflowDefinition> resolveWorkflow)
    : ICaseReviewTransactionStore
{
    private readonly NpgsqlDataSource source = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    private readonly Func<string, int, WorkflowDefinition> resolver = resolveWorkflow ?? throw new ArgumentNullException(nameof(resolveWorkflow));

    public async Task InitializeAsync(CaseReviewState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var json = CaseReviewStateJson.Serialize(state);
        if (state.Audit.Events.Count != 1) throw new CaseReviewBindingException();
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, cancellationToken);
            var connection = session.Connection;
            var transaction = session.Transaction;
            await Lock(connection, transaction, state.Process.CaseId, cancellationToken);
            if (await Read(connection, transaction, state.Process.CaseId, cancellationToken) is not null)
                throw new CaseReviewConflictException();
            await VerifyAssessment(connection, transaction, state, cancellationToken);
            await Insert(connection, transaction, state, 0, json, cancellationToken);
            await session.CommitAsync(cancellationToken);
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    // Fresh aggregate and its original assessment commit together. A duplicate initialization
    // returns committed history, including subsequent human review, without overwriting it.
    public async Task<CaseReviewState> InitializeRecordedAsync(CaseReviewState state, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Audit.Events.Count != 1) throw new CaseReviewBindingException();
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection;
            var transaction = session.Transaction;
            await Lock(connection, transaction, state.Process.CaseId, token);
            var existing = await Read(connection, transaction, state.Process.CaseId, token);
            if (existing is not null)
            {
                if (AssessmentRecordJson.Serialize(existing.Value.State.Assessment) != AssessmentRecordJson.Serialize(state.Assessment))
                    throw new CaseReviewConflictException();
                return existing.Value.State;
            }
            await PostgresAssessmentRecordStore.AppendOnConnectionAsync(state.Assessment, connection, transaction, token);
            await Insert(connection, transaction, state, 0, CaseReviewStateJson.Serialize(state), token);
            await session.CommitAsync(token);
            return state;
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<IReadOnlyList<CaseId>> ListCaseIdsAsync(CancellationToken token = default)
    {
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection;
            await using var command = new NpgsqlCommand("SELECT DISTINCT case_id FROM normacase.case_review_versions ORDER BY case_id LIMIT 501", connection);
            await using var reader = await command.ExecuteReaderAsync(token);
            var ids = new List<CaseId>();
            while (await reader.ReadAsync(token)) ids.Add(new(reader.GetString(0)));
            if (ids.Count > 500) throw new CaseReviewStorageException();
            return ids.AsReadOnly();
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<IReadOnlyList<CaseId>> ListCasePageAsync(int pageSize, string? afterCaseId,
        IReadOnlyCollection<CaseId>? permittedCases, CancellationToken token = default)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (afterCaseId is { Length: > 128 } || afterCaseId is "") throw new ArgumentException("Invalid case cursor.", nameof(afterCaseId));
        var scope = permittedCases?.Select(id => id.IsEmpty ? throw new ArgumentException("Empty case scope.", nameof(permittedCases)) : id.Value).ToArray();
        if (scope?.Length > 500) throw new ArgumentException("Case scope exceeds limit.", nameof(permittedCases));
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection;
            await using var command = new NpgsqlCommand(
                "SELECT DISTINCT case_id COLLATE \"C\" FROM normacase.case_review_versions WHERE ($1::text IS NULL OR case_id COLLATE \"C\" > $1 COLLATE \"C\") AND ($2::text[] IS NULL OR case_id = ANY($2)) ORDER BY case_id COLLATE \"C\" LIMIT $3", connection);
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)afterCaseId ?? DBNull.Value });
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text, Value = (object?)scope ?? DBNull.Value });
            command.Parameters.AddWithValue(pageSize + 1);
            await using var reader = await command.ExecuteReaderAsync(token);
            var ids = new List<CaseId>();
            while (await reader.ReadAsync(token)) ids.Add(new(reader.GetString(0)));
            return ids.AsReadOnly();
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<CaseReviewState> ExecuteAsync(CaseId caseId, Func<CaseReviewState, CaseReviewState> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.", nameof(caseId));
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, cancellationToken);
            var connection = session.Connection;
            var transaction = session.Transaction;
            await Lock(connection, transaction, caseId, cancellationToken);
            var current = await Read(connection, transaction, caseId, cancellationToken) ?? throw new CaseReviewBindingException();
            var next = update(current.State);
            VerifyAppend(current.State, next);
            var json = CaseReviewStateJson.Serialize(next);
            cancellationToken.ThrowIfCancellationRequested();
            await Insert(connection, transaction, next, checked(current.Version + 1), json, cancellationToken);
            await session.CommitAsync(cancellationToken);
            return next;
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    public async Task<CaseReviewState?> LoadAsync(CaseId caseId, CancellationToken cancellationToken = default)
    {
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.", nameof(caseId));
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, cancellationToken);
            var connection = session.Connection;
            var transaction = session.Transaction;
            var result = await Read(connection, transaction, caseId, cancellationToken);
            return result?.State;
        }
        catch (NpgsqlException) { throw new CaseReviewStorageException(); }
    }

    private async Task<(long Version, CaseReviewState State)?> Read(NpgsqlConnection connection, NpgsqlTransaction transaction, CaseId caseId, CancellationToken token)
    {
        var rows = new List<(long Version, string AssessmentId, string Json, string Hash)>();
        await using (var command = new NpgsqlCommand("SELECT version, assessment_id, state_json::text, state_sha256 FROM normacase.case_review_versions WHERE case_id=$1 ORDER BY version", connection, transaction))
        {
            command.Parameters.AddWithValue(caseId.Value);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        CaseReviewState? previous = null;
        long expectedVersion = 0;
        foreach (var row in rows)
        {
            if (row.Version != expectedVersion++ || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(row.Hash), Encoding.ASCII.GetBytes(Hash(row.Json)))) throw new CaseReviewIntegrityException();
            CaseReviewState state;
            try { state = CaseReviewStateJson.Deserialize(row.Json, resolver); }
            catch (Exception exception) when (exception is JsonException or ArgumentException) { throw new CaseReviewIntegrityException(); }
            if (state.Process.CaseId != caseId || state.Assessment.AssessmentId.Value != row.AssessmentId) throw new CaseReviewIntegrityException();
            if (previous is null)
            {
                if (state.Audit.Events.Count != 1) throw new CaseReviewIntegrityException();
                await VerifyAssessment(connection, transaction, state, token);
            }
            else if (previous.Assessment.AssessmentId != state.Assessment.AssessmentId)
                await VerifyCorrection(connection, transaction, previous, state, row.Version, token);
            else VerifyAppend(previous, state);
            previous = state;
        }
        return previous is null ? null : (rows[^1].Version, previous);
    }

    private void VerifyAppend(CaseReviewState previous, CaseReviewState next)
    {
        if (AssessmentRecordJson.Serialize(previous.Assessment) != AssessmentRecordJson.Serialize(next.Assessment)
            || previous.AssessmentCaseRevision != next.AssessmentCaseRevision
            || previous.Process.CaseId != next.Process.CaseId || previous.Process.CaseRevision != next.Process.CaseRevision
            || previous.Process.WorkflowId != next.Process.WorkflowId || previous.Process.WorkflowVersion != next.Process.WorkflowVersion
            || previous.Process.Revision == long.MaxValue
            || next.Process.Revision != previous.Process.Revision + 1
            || next.Audit.Events.Count != previous.Audit.Events.Count + 1)
            throw new CaseReviewIntegrityException();
        var definition = resolver(previous.Process.WorkflowId, previous.Process.WorkflowVersion);
        if (!definition.Transitions.Any(item => item.FromStateId == previous.Process.StateId && item.ToStateId == next.Process.StateId))
            throw new CaseReviewIntegrityException();
        var prefix = NormaCase.Domain.Audit.AssessmentAuditTrail.Start(next.Audit.Events[0]);
        foreach (var item in next.Audit.Events.Skip(1).Take(previous.Audit.Events.Count - 1)) prefix = prefix.Append(item);
        if (AssessmentAuditJson.Serialize(prefix) != AssessmentAuditJson.Serialize(previous.Audit)) throw new CaseReviewIntegrityException();
    }

    private static async Task VerifyAssessment(NpgsqlConnection connection, NpgsqlTransaction transaction, CaseReviewState state, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT case_id, record_json::text, record_sha256, knowledge_pack_id, knowledge_release, platform_version, assessment_date, recorded_at_utc_ticks, record_format_version, recorded_at_utc FROM normacase.assessment_records WHERE assessment_id=$1", connection, transaction);
        command.Parameters.AddWithValue(state.Assessment.AssessmentId.Value);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token) || reader.GetString(0) != state.Process.CaseId.Value
            || reader.GetString(1) != AssessmentRecordJson.Serialize(state.Assessment)
            || reader.GetString(2) != Hash(reader.GetString(1))
            || reader.GetString(3) != state.Assessment.KnowledgePackId
            || reader.GetString(4) != state.Assessment.Result.KnowledgeRelease
            || reader.GetString(5) != state.Assessment.PlatformVersion
            || reader.GetFieldValue<DateOnly>(6) != state.Assessment.Input.AssessmentDate
            || reader.GetInt64(7) != state.Assessment.RecordedAtUtc.Ticks
            || reader.GetInt32(8) != AssessmentRecordJson.CurrentFormatVersion
            || reader.GetDateTime(9).Ticks != state.Assessment.RecordedAtUtc.Ticks - state.Assessment.RecordedAtUtc.Ticks % 10) throw new CaseReviewIntegrityException();
    }

    private static async Task Lock(NpgsqlConnection connection, NpgsqlTransaction transaction, CaseId caseId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1, 0))", connection, transaction);
        command.Parameters.AddWithValue("normacase.case-review:" + caseId.Value);
        await command.ExecuteNonQueryAsync(token);
    }
    private static async Task Insert(NpgsqlConnection connection, NpgsqlTransaction transaction, CaseReviewState state, long version, string json, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("INSERT INTO normacase.case_review_versions(case_id,version,assessment_id,state_json,state_sha256) VALUES($1,$2,$3,$4,$5)", connection, transaction);
        command.Parameters.AddWithValue(state.Process.CaseId.Value);
        command.Parameters.AddWithValue(version);
        command.Parameters.AddWithValue(state.Assessment.AssessmentId.Value);
        command.Parameters.AddWithValue(NpgsqlDbType.Json, json);
        command.Parameters.AddWithValue(Hash(json));
        await command.ExecuteNonQueryAsync(token);
    }
    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
