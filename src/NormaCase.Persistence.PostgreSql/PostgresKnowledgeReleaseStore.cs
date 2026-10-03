using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Knowledge;
using NormaCase.Knowledge.Catalog;
using NormaCase.Knowledge.Validation;

namespace NormaCase.Persistence.PostgreSql;

public sealed class KnowledgeReleaseStorageException : Exception
{
    public KnowledgeReleaseStorageException() : base("Knowledge release storage operation failed.") { }
}
public sealed class KnowledgeReleaseIntegrityException : Exception
{
    public KnowledgeReleaseIntegrityException() : base("Stored knowledge release failed verification.") { }
}

public sealed class PostgresKnowledgeReleaseStore(NpgsqlDataSource dataSource) : IKnowledgeReleaseStore
{
    private readonly NpgsqlDataSource source = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async Task<KnowledgeReleaseArtifact> RegisterAsync(string knowledgePackJson, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(knowledgePackJson);
        if (new UTF8Encoding(false, true).GetByteCount(knowledgePackJson) > 1048576)
            throw new ArgumentException("Knowledge release exceeds storage limit.", nameof(knowledgePackJson));
        var artifact = new KnowledgeReleaseCatalog().Register(knowledgePackJson);
        Identity(artifact.PackId); Identity(artifact.ReleaseId);
        try
        {
            await using var connection = await source.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using (var command = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_release_artifacts
                    (pack_id,release_id,lifecycle_status,validation_level,pack_json,pack_sha256)
                VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (pack_id,release_id) DO NOTHING
                """, connection, transaction))
            {
                command.Parameters.AddWithValue(artifact.PackId);
                command.Parameters.AddWithValue(artifact.ReleaseId);
                command.Parameters.AddWithValue(artifact.LifecycleStatus);
                command.Parameters.AddWithValue(artifact.ValidationLevel);
                command.Parameters.AddWithValue(NpgsqlDbType.Json, artifact.KnowledgePackJson);
                command.Parameters.AddWithValue(artifact.Sha256);
                await command.ExecuteNonQueryAsync(token);
            }
            var stored = await Read(connection, transaction, artifact.PackId, artifact.ReleaseId, token)
                ?? throw new KnowledgeReleaseIntegrityException();
            if (stored.KnowledgePackJson != artifact.KnowledgePackJson)
                throw new KnowledgeReleaseIdentityConflictException(artifact.PackId, artifact.ReleaseId);
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(token);
            return stored;
        }
        catch (NpgsqlException) { throw new KnowledgeReleaseStorageException(); }
    }

    public async Task<KnowledgeReleaseArtifact?> LoadAsync(string packId, string releaseId, CancellationToken token = default)
    {
        Identity(packId); Identity(releaseId);
        try
        {
            await using var connection = await source.OpenConnectionAsync(token);
            return await Read(connection, null, packId, releaseId, token);
        }
        catch (NpgsqlException) { throw new KnowledgeReleaseStorageException(); }
    }

    private static async Task<KnowledgeReleaseArtifact?> Read(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string packId, string releaseId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT pack_json::text,pack_sha256,lifecycle_status,validation_level
            FROM normacase.knowledge_release_artifacts WHERE pack_id=$1 AND release_id=$2
            """, connection, transaction);
        command.Parameters.AddWithValue(packId); command.Parameters.AddWithValue(releaseId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        try
        {
            var json = reader.GetString(0);
            if (new UTF8Encoding(false, true).GetByteCount(json) > 1048576
                || Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != reader.GetString(1))
                throw new KnowledgeReleaseIntegrityException();
            var artifact = new KnowledgeReleaseCatalog().Register(json);
            if (artifact.PackId != packId || artifact.ReleaseId != releaseId || artifact.Sha256 != reader.GetString(1)
                || artifact.LifecycleStatus != reader.GetString(2) || artifact.ValidationLevel != reader.GetString(3))
                throw new KnowledgeReleaseIntegrityException();
            return artifact;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or KnowledgeValidationException)
        { throw new KnowledgeReleaseIntegrityException(); }
    }

    private static void Identity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256) throw new ArgumentException("Knowledge identity exceeds storage limit.");
    }
}
