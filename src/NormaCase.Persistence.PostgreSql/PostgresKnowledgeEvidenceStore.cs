using Npgsql;
using NormaCase.Application.Knowledge;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresKnowledgeEvidenceStore(NpgsqlDataSource dataSource) : IKnowledgeEvidenceStore
{
    public async Task<KnowledgeEvidenceArtifact> RegisterAsync(KnowledgeEvidenceArtifact artifact, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(dataSource, token);
            var connection = session.Connection;
            var transaction = session.Transaction;
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_evidence_artifacts
                (evidence_id,kind,title,content,content_sha256,recorded_by_actor_id,recorded_at_utc)
                VALUES ($1,$2,$3,$4,$5,$6,$7) ON CONFLICT (evidence_id) DO NOTHING
                """, connection, transaction))
            {
                foreach (var value in new object[] { artifact.EvidenceId, artifact.Kind, artifact.Title, artifact.Content,
                    artifact.Sha256, artifact.RecordedByActorId, artifact.RecordedAtUtc }) insert.Parameters.AddWithValue(value);
                await insert.ExecuteNonQueryAsync(token);
            }
            var stored = await Read(connection, transaction, artifact.EvidenceId, token)
                ?? throw new KnowledgeEvidenceIntegrityException();
            if (stored != artifact) throw new KnowledgeGovernanceConflictException();
            token.ThrowIfCancellationRequested();
            await session.CommitAsync(token);
            return stored;
        }
        catch (NpgsqlException) { throw new KnowledgeGovernanceStorageException(); }
    }
    public async Task<KnowledgeEvidenceArtifact?> LoadAsync(string evidenceId, CancellationToken token = default)
    {
        KnowledgeGovernanceValidation.Text(evidenceId);
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(dataSource, token);
            var connection = session.Connection;
            return await Read(connection, null, evidenceId, token);
        }
        catch (NpgsqlException) { throw new KnowledgeGovernanceStorageException(); }
    }
    internal static async Task<KnowledgeEvidenceArtifact?> Read(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string evidenceId, CancellationToken token)
    {
        await using var select = new NpgsqlCommand("""
            SELECT kind,title,content,content_sha256,recorded_by_actor_id,recorded_at_utc
            FROM normacase.knowledge_evidence_artifacts WHERE evidence_id=$1
            """, connection, transaction);
        select.Parameters.AddWithValue(evidenceId);
        await using var reader = await select.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        try
        {
            var artifact = new KnowledgeEvidenceArtifact(evidenceId, reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(4), new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
            if (artifact.Sha256 != reader.GetString(3)) throw new KnowledgeEvidenceIntegrityException();
            return artifact;
        }
        catch (ArgumentException) { throw new KnowledgeEvidenceIntegrityException(); }
    }
    internal static async Task Verify(NpgsqlConnection connection, NpgsqlTransaction transaction,
        KnowledgeChangeProposal proposal, CancellationToken token)
    {
        foreach (var (id, kind) in new[] { (proposal.SourceReference, "SOURCE"), (proposal.ImpactReference, "IMPACT"), (proposal.TestReference, "TESTS") })
        {
            var artifact = await Read(connection, transaction, id, token);
            if (artifact is null || artifact.Kind != kind || artifact.RecordedAtUtc > proposal.ProposedAtUtc)
                throw new KnowledgeGovernanceConflictException();
        }
    }
}
