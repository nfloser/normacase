using Npgsql;
using NormaCase.Application.Knowledge;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresReviewedKnowledgeActivationStore(NpgsqlDataSource dataSource, bool requireRetainedEvidence = false)
    : IReviewedKnowledgeActivationStore
{
    public async Task<KnowledgeChangeRecord> ProposeAsync(KnowledgeChangeProposal proposal, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return await Write(async (connection, transaction) =>
        {
            await VerifyRelease(connection, transaction, proposal, token);
            if (requireRetainedEvidence) await PostgresKnowledgeEvidenceStore.Verify(connection, transaction, proposal, token);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_changes
                (change_id,pack_id,release_id,pack_sha256,source_reference,impact_reference,test_reference,proposer_actor_id,proposed_at_utc)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
                """, connection, transaction);
            Add(insert, proposal.ChangeId, proposal.PackId, proposal.ReleaseId, proposal.Sha256,
                proposal.SourceReference, proposal.ImpactReference, proposal.TestReference,
                proposal.ProposerActorId, proposal.ProposedAtUtc);
            await insert.ExecuteNonQueryAsync(token);
            return new KnowledgeChangeRecord(proposal, null);
        }, token);
    }

    public async Task<KnowledgeChangeRecord> DecideAsync(KnowledgeChangeDecision decision, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return await Write(async (connection, transaction) =>
        {
            var existing = await ReadChange(connection, transaction, decision.ChangeId, true, token)
                ?? throw new KnowledgeGovernanceNotFoundException();
            if (existing.Decision is not null || existing.Proposal.ProposerActorId == decision.ReviewerActorId
                || decision.ReviewedAtUtc < existing.Proposal.ProposedAtUtc)
                throw new KnowledgeGovernanceConflictException();
            await VerifyRelease(connection, transaction, existing.Proposal, token);
            if (requireRetainedEvidence) await PostgresKnowledgeEvidenceStore.Verify(connection, transaction, existing.Proposal, token);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_change_decisions
                (change_id,reviewer_actor_id,reviewed_at_utc,approved,reason) VALUES ($1,$2,$3,$4,$5)
                """, connection, transaction);
            Add(insert, decision.ChangeId, decision.ReviewerActorId, decision.ReviewedAtUtc, decision.Approved, decision.Reason);
            await insert.ExecuteNonQueryAsync(token);
            return new KnowledgeChangeRecord(existing.Proposal, decision);
        }, token);
    }

    public async Task<KnowledgeActivationRecord> ActivateAsync(KnowledgeActivationCommand command, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await Write(async (connection, transaction) =>
        {
            var change = await ReadChange(connection, transaction, command.ChangeId, false, token)
                ?? throw new KnowledgeGovernanceNotFoundException();
            if (change.Decision is not { Approved: true } || command.ActivatedAtUtc < change.Decision.ReviewedAtUtc)
                throw new KnowledgeGovernanceConflictException();
            // Serializes competing changes for the same pack, including the initial empty history.
            await using (var packLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1, 184));", connection, transaction))
            {
                Add(packLock, change.Proposal.PackId);
                await packLock.ExecuteNonQueryAsync(token);
            }
            var current = await ReadActivation(connection, transaction, change.Proposal.PackId, null, token);
            if ((current?.Revision ?? 0) != command.ExpectedRevision
                || (current is not null && command.ActivatedAtUtc < current.ActivatedAtUtc))
                throw new KnowledgeGovernanceConflictException();
            await VerifyRelease(connection, transaction, change.Proposal, token);
            if (requireRetainedEvidence) await PostgresKnowledgeEvidenceStore.Verify(connection, transaction, change.Proposal, token);
            var result = new KnowledgeActivationRecord(change.Proposal.PackId, command.ExpectedRevision + 1,
                command.ChangeId, change.Proposal.ReleaseId, change.Proposal.Sha256, command.ActorId, command.ActivatedAtUtc);
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.knowledge_activations(pack_id,revision,change_id,actor_id,activated_at_utc)
                VALUES ($1,$2,$3,$4,$5)
                """, connection, transaction);
            Add(insert, result.PackId, result.Revision, result.ChangeId, result.ActorId, result.ActivatedAtUtc);
            await insert.ExecuteNonQueryAsync(token);
            return result;
        }, token);
    }

    public async Task<IReadOnlyList<KnowledgeChangeRecord>> ListChangesAsync(int pageSize, string? afterChangeId = null, CancellationToken token = default)
    {
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (afterChangeId is not null) KnowledgeGovernanceValidation.Text(afterChangeId);
        return await Read(async connection =>
        {
            var ids = new List<string>();
            await using (var query = new NpgsqlCommand(
                "SELECT change_id FROM normacase.knowledge_changes"
                + (afterChangeId is null ? "" : " WHERE change_id COLLATE \"C\">$2 COLLATE \"C\"")
                + " ORDER BY change_id COLLATE \"C\" LIMIT $1", connection))
            {
                Add(query, pageSize);
                if (afterChangeId is not null) Add(query, afterChangeId);
                await using var reader = await query.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) ids.Add(reader.GetString(0));
            }
            var records = new List<KnowledgeChangeRecord>();
            foreach (var id in ids)
                records.Add((await ReadChange(connection, null, id, false, token))
                    ?? throw new KnowledgeGovernanceNotFoundException());
            return (IReadOnlyList<KnowledgeChangeRecord>)records.AsReadOnly();
        }, token);
    }

    public async Task<KnowledgeChangeRecord?> LoadChangeAsync(string changeId, CancellationToken token = default)
    {
        KnowledgeGovernanceValidation.Text(changeId);
        return await Read(connection => ReadChange(connection, null, changeId, false, token), token);
    }
    public async Task<KnowledgeActivationRecord?> LoadActiveAsync(string packId, CancellationToken token = default)
    {
        KnowledgeGovernanceValidation.Text(packId);
        return await Read(connection => ReadActivation(connection, null, packId, null, token), token);
    }
    public async Task<KnowledgeActivationRecord?> LoadActivationAsync(string packId, long revision, CancellationToken token = default)
    {
        KnowledgeGovernanceValidation.Text(packId);
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        return await Read(connection => ReadActivation(connection, null, packId, revision, token), token);
    }

    private async Task<T> Write<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operation, CancellationToken token)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            var result = await operation(connection, transaction);
            token.ThrowIfCancellationRequested();
            await transaction.CommitAsync(token);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new KnowledgeGovernanceConflictException(); }
        catch (NpgsqlException) { throw new KnowledgeGovernanceStorageException(); }
    }
    private async Task<T> Read<T>(Func<NpgsqlConnection, Task<T>> operation, CancellationToken token)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(token);
            return await operation(connection);
        }
        catch (NpgsqlException) { throw new KnowledgeGovernanceStorageException(); }
    }
    private static async Task VerifyRelease(NpgsqlConnection connection, NpgsqlTransaction transaction,
        KnowledgeChangeProposal proposal, CancellationToken token)
    {
        var artifact = await PostgresKnowledgeReleaseStore.Read(connection, transaction, proposal.PackId, proposal.ReleaseId, token)
            ?? throw new KnowledgeGovernanceNotFoundException();
        if (artifact.Sha256 != proposal.Sha256) throw new KnowledgeGovernanceConflictException();
    }
    private static async Task<KnowledgeChangeRecord?> ReadChange(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string changeId, bool lockRow, CancellationToken token)
    {
        KnowledgeChangeProposal proposal;
        await using (var select = new NpgsqlCommand("""
            SELECT pack_id,release_id,pack_sha256,source_reference,impact_reference,test_reference,proposer_actor_id,proposed_at_utc
            FROM normacase.knowledge_changes WHERE change_id=$1
            """ + (lockRow ? " FOR UPDATE" : ""), connection, transaction))
        {
            Add(select, changeId);
            await using var reader = await select.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return null;
            proposal = new(changeId, reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), Utc(reader.GetDateTime(7)));
        }
        await using var decisionQuery = new NpgsqlCommand("""
            SELECT reviewer_actor_id,reviewed_at_utc,approved,reason
            FROM normacase.knowledge_change_decisions WHERE change_id=$1
            """, connection, transaction);
        Add(decisionQuery, changeId);
        await using var decisionReader = await decisionQuery.ExecuteReaderAsync(token);
        return new(proposal, await decisionReader.ReadAsync(token)
            ? new(changeId, decisionReader.GetString(0), Utc(decisionReader.GetDateTime(1)),
                decisionReader.GetBoolean(2), decisionReader.GetString(3)) : null);
    }
    private static async Task<KnowledgeActivationRecord?> ReadActivation(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string packId, long? revision, CancellationToken token)
    {
        await using var select = new NpgsqlCommand("""
            SELECT a.revision,a.change_id,c.release_id,c.pack_sha256,a.actor_id,a.activated_at_utc
            FROM normacase.knowledge_activations a JOIN normacase.knowledge_changes c ON c.change_id=a.change_id
            WHERE a.pack_id=$1
            """ + (revision.HasValue ? " AND a.revision=$2" : " ORDER BY a.revision DESC LIMIT 1"), connection, transaction);
        Add(select, packId);
        if (revision.HasValue) Add(select, revision.Value);
        await using var reader = await select.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(packId, reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), Utc(reader.GetDateTime(5))) : null;
    }
    private static void Add(NpgsqlCommand command, params object[] values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
