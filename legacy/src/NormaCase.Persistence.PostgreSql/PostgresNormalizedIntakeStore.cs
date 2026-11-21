using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Intake;
using NormaCase.Serialization;
namespace NormaCase.Persistence.PostgreSql;

public sealed class IntakeStorageException : Exception { public IntakeStorageException() : base("Normalized intake storage failed.") { } }
public sealed class IntakeIntegrityException : Exception { public IntakeIntegrityException() : base("Normalized intake history failed verification.") { } }
public sealed class PostgresNormalizedIntakeStore(NpgsqlDataSource source) : INormalizedIntakeStore
{
    private readonly NpgsqlDataSource source = source ?? throw new ArgumentNullException(nameof(source));
    public async Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(record); var provenance = record.Provenance;
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection;
            var transaction = session.Transaction;
            // One bounded synthetic host: a global lock also protects cross-stream CaseId ownership.
            await Execute(connection, transaction, "SELECT pg_advisory_xact_lock(hashtextextended('normacase.intake',0))", token);
            await using (var lookup = new NpgsqlCommand("SELECT upstream_case_id,upstream_revision FROM normacase.intake_messages WHERE source_system_id=$1 AND message_id=$2", connection, transaction))
            {
                lookup.Parameters.AddWithValue(provenance.SourceSystemId); lookup.Parameters.AddWithValue(provenance.MessageId);
                await using var reader = await lookup.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token) && (reader.GetString(0) != provenance.UpstreamCaseId || reader.GetInt64(1) != provenance.UpstreamRevision)) throw new IntakeConflictException();
            }
            var existing = await Read(connection, transaction, provenance.SourceSystemId, provenance.UpstreamCaseId, provenance.UpstreamRevision, token);
            if (existing is not null)
            {
                if (!existing.HasSameContent(record)) throw new IntakeConflictException();
                await Alias(connection, transaction, provenance, token); await session.CommitAsync(token);
                return new(IntakeAcceptance.Duplicate, existing);
            }
            await using (var ownership = new NpgsqlCommand("SELECT source_system_id,upstream_case_id,case_id,case_type_id FROM normacase.intake_streams WHERE (source_system_id=$1 AND upstream_case_id=$2) OR case_id=$3", connection, transaction))
            {
                ownership.Parameters.AddWithValue(provenance.SourceSystemId); ownership.Parameters.AddWithValue(provenance.UpstreamCaseId); ownership.Parameters.AddWithValue(record.CaseId.Value);
                await using var reader = await ownership.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                    if (reader.GetString(0) != provenance.SourceSystemId || reader.GetString(1) != provenance.UpstreamCaseId || reader.GetString(2) != record.CaseId.Value || reader.GetString(3) != record.CaseTypeId) throw new IntakeConflictException();
            }
            await using (var latest = new NpgsqlCommand("SELECT max(upstream_revision) FROM normacase.intake_records WHERE source_system_id=$1 AND upstream_case_id=$2", connection, transaction))
            {
                latest.Parameters.AddWithValue(provenance.SourceSystemId); latest.Parameters.AddWithValue(provenance.UpstreamCaseId);
                var revision = await latest.ExecuteScalarAsync(token); if (revision is long current && provenance.UpstreamRevision <= current) throw new IntakeConflictException();
            }
            await using (var stream = new NpgsqlCommand("INSERT INTO normacase.intake_streams VALUES($1,$2,$3,$4) ON CONFLICT(source_system_id,upstream_case_id) DO NOTHING", connection, transaction))
            {
                stream.Parameters.AddWithValue(provenance.SourceSystemId); stream.Parameters.AddWithValue(provenance.UpstreamCaseId); stream.Parameters.AddWithValue(record.CaseId.Value); stream.Parameters.AddWithValue(record.CaseTypeId); await stream.ExecuteNonQueryAsync(token);
            }
            var json = NormalizedIntakeJson.Serialize(record);
            await using (var insert = new NpgsqlCommand("INSERT INTO normacase.intake_records VALUES($1,$2,$3,$4,$5)", connection, transaction))
            {
                insert.Parameters.AddWithValue(provenance.SourceSystemId); insert.Parameters.AddWithValue(provenance.UpstreamCaseId); insert.Parameters.AddWithValue(provenance.UpstreamRevision); insert.Parameters.AddWithValue(NpgsqlDbType.Json, json); insert.Parameters.AddWithValue(Hash(json)); await insert.ExecuteNonQueryAsync(token);
            }
            await Alias(connection, transaction, provenance, token); await session.CommitAsync(token);
            return new(IntakeAcceptance.Accepted, record);
        }
        catch (NpgsqlException) { throw new IntakeStorageException(); }
    }
    public async Task<NormalizedIntakeRecord?> LoadAsync(string sourceSystemId, string upstreamCaseId, long upstreamRevision, CancellationToken token = default)
    {
        // Reuse boundary validation without touching knowledge or input values.
        _ = new IntakeProvenance(sourceSystemId, upstreamCaseId, "load", upstreamRevision, "load", 1, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));
        try { await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection; return await Read(connection, null, sourceSystemId, upstreamCaseId, upstreamRevision, token); }
        catch (NpgsqlException) { throw new IntakeStorageException(); }
    }
    public async Task<NormalizedIntakeRecord?> LoadForCaseAsync(NormaCase.Domain.Cases.CaseId caseId, CancellationToken token = default)
    {
        if (caseId.IsEmpty) throw new ArgumentException("Explicit case identity required.");
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            var connection = session.Connection;
            return await LoadForCaseOnConnectionAsync(connection, session.Transaction, caseId, token);
        }
        catch (NpgsqlException) { throw new IntakeStorageException(); }
    }
    internal static async Task<NormalizedIntakeRecord?> LoadForCaseOnConnectionAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, NormaCase.Domain.Cases.CaseId caseId, CancellationToken token)
    {
            string system; string upstream; long revision;
            await using (var command = new NpgsqlCommand("SELECT s.source_system_id,s.upstream_case_id,max(r.upstream_revision) FROM normacase.intake_streams s JOIN normacase.intake_records r USING(source_system_id,upstream_case_id) WHERE s.case_id=$1 GROUP BY s.source_system_id,s.upstream_case_id", connection, transaction))
            {
                command.Parameters.AddWithValue(caseId.Value);
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) return null;
                system = reader.GetString(0); upstream = reader.GetString(1); revision = reader.GetInt64(2);
            }
            return await Read(connection, transaction, system, upstream, revision, token);
    }
    internal static async Task<NormalizedIntakeRecord?> Read(NpgsqlConnection connection, NpgsqlTransaction? transaction, string system, string upstream, long revision, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT r.record_json::text,r.record_sha256,s.case_id,s.case_type_id FROM normacase.intake_records r JOIN normacase.intake_streams s USING(source_system_id,upstream_case_id) WHERE r.source_system_id=$1 AND r.upstream_case_id=$2 AND r.upstream_revision=$3", connection, transaction);
        command.Parameters.AddWithValue(system); command.Parameters.AddWithValue(upstream); command.Parameters.AddWithValue(revision);
        await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) return null;
        var json = reader.GetString(0); if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(reader.GetString(1)), Encoding.ASCII.GetBytes(Hash(json)))) throw new IntakeIntegrityException();
        NormalizedIntakeRecord record; try { record = NormalizedIntakeJson.Deserialize(json); } catch (Exception exception) when (exception is JsonException or ArgumentException) { throw new IntakeIntegrityException(); }
        if (record.Provenance.SourceSystemId != system || record.Provenance.UpstreamCaseId != upstream || record.Provenance.UpstreamRevision != revision || record.CaseId.Value != reader.GetString(2) || record.CaseTypeId != reader.GetString(3)) throw new IntakeIntegrityException(); return record;
    }
    private static async Task Alias(NpgsqlConnection connection, NpgsqlTransaction transaction, IntakeProvenance provenance, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("INSERT INTO normacase.intake_messages VALUES($1,$2,$3,$4) ON CONFLICT(source_system_id,message_id) DO NOTHING", connection, transaction);
        command.Parameters.AddWithValue(provenance.SourceSystemId); command.Parameters.AddWithValue(provenance.MessageId); command.Parameters.AddWithValue(provenance.UpstreamCaseId); command.Parameters.AddWithValue(provenance.UpstreamRevision); await command.ExecuteNonQueryAsync(token);
    }
    private static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken token) { await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(token); }
    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
