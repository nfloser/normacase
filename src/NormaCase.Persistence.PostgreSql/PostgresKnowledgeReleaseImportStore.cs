using Npgsql;
using NormaCase.Application.Knowledge;
using NormaCase.Knowledge.Catalog;

namespace NormaCase.Persistence.PostgreSql;

public sealed record KnowledgeReleaseImportRecord(KnowledgeReleaseArtifact Artifact,
    string ImportedByActorId, DateTimeOffset ImportedAtUtc);

public sealed class PostgresKnowledgeReleaseImportStore(NpgsqlDataSource source)
{
    public async Task<KnowledgeReleaseImportRecord> ImportAsync(string json, DateTimeOffset importedAtUtc,
        CancellationToken token = default)
    {
        KnowledgeGovernanceValidation.Utc(importedAtUtc);
        var context = PostgresAuthorizedOperation.Current
            ?? throw new InvalidOperationException("Verified authorized import transaction required.");
        if (!ReferenceEquals(source, context.Source)) throw new InvalidOperationException("Import cannot cross data sources.");
        var artifact = await new PostgresKnowledgeReleaseStore(source).RegisterAsync(json, token);
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_release_imports(pack_id,release_id,actor_id,imported_at_utc,pack_sha256)
                VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING;
                """, context.Connection, context.Transaction);
            foreach (var value in new object[] { artifact.PackId, artifact.ReleaseId, context.State.ActorId,
                importedAtUtc.UtcDateTime, artifact.Sha256 }) insert.Parameters.AddWithValue(value);
            await insert.ExecuteNonQueryAsync(token);
            return await Read(context.Connection, context.Transaction, artifact, token)
                ?? throw new KnowledgeReleaseIntegrityException();
        }
        catch (NpgsqlException) { throw new KnowledgeReleaseStorageException(); }
    }

    public async Task<KnowledgeReleaseImportRecord?> LoadAsync(string packId, string releaseId, CancellationToken token = default)
    {
        var artifact = await new PostgresKnowledgeReleaseStore(source).LoadAsync(packId, releaseId, token);
        if (artifact is null) return null;
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, token);
            return await Read(session.Connection, session.Transaction, artifact, token);
        }
        catch (NpgsqlException) { throw new KnowledgeReleaseStorageException(); }
    }

    private static async Task<KnowledgeReleaseImportRecord?> Read(NpgsqlConnection connection, NpgsqlTransaction transaction,
        KnowledgeReleaseArtifact artifact, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT actor_id,imported_at_utc,pack_sha256 FROM normacase.knowledge_release_imports
            WHERE pack_id=$1 AND release_id=$2;
            """, connection, transaction);
        command.Parameters.AddWithValue(artifact.PackId); command.Parameters.AddWithValue(artifact.ReleaseId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        if (reader.GetString(2) != artifact.Sha256) throw new KnowledgeReleaseIntegrityException();
        try
        {
            var actor = KnowledgeGovernanceValidation.Text(reader.GetString(0));
            return new(artifact, actor, new(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc)));
        }
        catch (ArgumentException) { throw new KnowledgeReleaseIntegrityException(); }
    }
}
