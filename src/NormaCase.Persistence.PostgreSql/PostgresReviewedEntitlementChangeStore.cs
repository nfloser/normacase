using Npgsql;
using NormaCase.Application.Authorization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresReviewedEntitlementChangeStore(
    NpgsqlDataSource dataSource) : IReviewedEntitlementChangeStore
{
    public async Task<EntitlementChangeRecord> ProposeAsync(
        EntitlementChangeProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                INSERT INTO normacase.identity_entitlement_changes(
                    change_id, target_actor_id, expected_entitlement_revision, actions,
                    case_ids, proposer_actor_id, proposed_at_utc, reason)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8);
                """);
            AddProposal(command, proposal);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new(proposal, null);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new EntitlementChangeConflictException();
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new EntitlementChangeStorageException();
        }
    }

    public async Task<EntitlementChangeRecord> DecideAsync(
        EntitlementChangeDecision decision, bool requireDistinctDecisionActor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var proposal = await LoadProposalAsync(connection, transaction, decision.ChangeId, true, cancellationToken)
                ?? throw new EntitlementChangeNotFoundException();
            if (requireDistinctDecisionActor
                && string.Equals(proposal.ProposerActorId, decision.DecisionActorId, StringComparison.Ordinal))
                throw new EntitlementSeparationOfDutiesException();
            if (decision.DecidedAtUtc < proposal.ProposedAtUtc)
                throw new EntitlementChangeConflictException();

            await using (var decisionLookup = new NpgsqlCommand(
                "SELECT 1 FROM normacase.identity_entitlement_decisions WHERE change_id=$1;",
                connection, transaction))
            {
                decisionLookup.Parameters.AddWithValue(decision.ChangeId);
                if (await decisionLookup.ExecuteScalarAsync(cancellationToken) is not null)
                    throw new EntitlementChangeConflictException();
            }

            if (decision.Approved)
            {
                await using (var actorLock = new NpgsqlCommand(
                    "SELECT pg_advisory_xact_lock(hashtextextended($1, 117));", connection, transaction))
                {
                    actorLock.Parameters.AddWithValue(proposal.TargetActorId);
                    await actorLock.ExecuteNonQueryAsync(cancellationToken);
                }
                var currentRevision = await CurrentRevisionAsync(
                    connection, transaction, proposal.TargetActorId, cancellationToken);
                if (currentRevision != proposal.ExpectedEntitlementRevision)
                    throw new EntitlementChangeConflictException();
                await using var version = new NpgsqlCommand(
                    """
                    INSERT INTO normacase.identity_entitlement_versions(
                        actor_id, revision, change_id, actions, case_ids)
                    VALUES ($1,$2,$3,$4,$5);
                    """, connection, transaction);
                version.Parameters.AddWithValue(proposal.TargetActorId);
                version.Parameters.AddWithValue(checked(currentRevision + 1));
                version.Parameters.AddWithValue(proposal.ChangeId);
                version.Parameters.AddWithValue(proposal.Actions.ToArray());
                version.Parameters.AddWithValue(proposal.CaseIds.ToArray());
                await version.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO normacase.identity_entitlement_decisions(
                    change_id, decision_actor_id, decided_at_utc, approved, reason)
                VALUES ($1,$2,$3,$4,$5);
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(decision.ChangeId);
                insert.Parameters.AddWithValue(decision.DecisionActorId);
                insert.Parameters.AddWithValue(decision.DecidedAtUtc);
                insert.Parameters.AddWithValue(decision.Approved);
                insert.Parameters.AddWithValue(decision.Reason);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(proposal, decision);
        }
        catch (Exception exception) when (exception is EntitlementChangeConflictException
            or EntitlementChangeNotFoundException or EntitlementSeparationOfDutiesException)
        {
            throw;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or OverflowException)
        {
            throw new EntitlementChangeStorageException();
        }
    }

    public async Task<EntitlementChangeRecord?> LoadChangeAsync(
        string changeId, CancellationToken cancellationToken = default)
    {
        EntitlementChangeProposal.ValidateIdentity(changeId, nameof(changeId));
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var proposal = await LoadProposalAsync(connection, null, changeId, false, cancellationToken);
            if (proposal is null) return null;
            await using var command = new NpgsqlCommand(
                """
                SELECT decision_actor_id, decided_at_utc, approved, reason
                FROM normacase.identity_entitlement_decisions WHERE change_id=$1;
                """, connection);
            command.Parameters.AddWithValue(changeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var decision = await reader.ReadAsync(cancellationToken)
                ? new EntitlementChangeDecision(changeId, reader.GetString(0), Utc(reader.GetDateTime(1)),
                    reader.GetBoolean(2), reader.GetString(3)) : null;
            return new(proposal, decision);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new EntitlementChangeStorageException();
        }
    }

    public async Task<IReadOnlyList<EntitlementChangeRecord>> ListPendingAsync(
        int limit, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT c.change_id, c.target_actor_id, c.expected_entitlement_revision,
                       c.actions, c.case_ids, c.proposer_actor_id, c.proposed_at_utc, c.reason
                FROM normacase.identity_entitlement_changes c
                LEFT JOIN normacase.identity_entitlement_decisions d ON d.change_id=c.change_id
                WHERE d.change_id IS NULL
                ORDER BY c.proposed_at_utc, c.change_id
                LIMIT $1;
                """);
            command.Parameters.AddWithValue(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<EntitlementChangeRecord>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var proposal = new EntitlementChangeProposal(
                    reader.GetString(0), reader.GetString(1), reader.GetInt64(2),
                    reader.GetFieldValue<string[]>(3), reader.GetFieldValue<string[]>(4),
                    reader.GetString(5), Utc(reader.GetDateTime(6)), reader.GetString(7));
                result.Add(new(proposal, null));
            }
            return result;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new EntitlementChangeStorageException();
        }
    }

    public async Task<IdentityEntitlementState> LoadEffectiveAsync(
        string actorId, CancellationToken cancellationToken = default)
    {
        EntitlementChangeProposal.ValidateIdentity(actorId, nameof(actorId));
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT revision, actions, case_ids
                FROM normacase.identity_entitlement_versions
                WHERE actor_id=$1 ORDER BY revision DESC LIMIT 1;
                """);
            command.Parameters.AddWithValue(actorId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? new(actorId, reader.GetInt64(0), reader.GetFieldValue<string[]>(1), reader.GetFieldValue<string[]>(2))
                : new(actorId, 0, [], []);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new EntitlementChangeStorageException();
        }
    }

    private static async Task<EntitlementChangeProposal?> LoadProposalAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string changeId,
        bool lockRow, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT target_actor_id, expected_entitlement_revision, actions, case_ids,
                   proposer_actor_id, proposed_at_utc, reason
            FROM normacase.identity_entitlement_changes
            WHERE change_id=$1
            """ + (lockRow ? " FOR UPDATE;" : ";"), connection, transaction);
        command.Parameters.AddWithValue(changeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new(changeId, reader.GetString(0), reader.GetInt64(1),
                reader.GetFieldValue<string[]>(2), reader.GetFieldValue<string[]>(3), reader.GetString(4),
                Utc(reader.GetDateTime(5)), reader.GetString(6)) : null;
    }

    private static async Task<long> CurrentRevisionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string actorId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(MAX(revision),0) FROM normacase.identity_entitlement_versions WHERE actor_id=$1;",
            connection, transaction);
        command.Parameters.AddWithValue(actorId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static void AddProposal(NpgsqlCommand command, EntitlementChangeProposal proposal)
    {
        command.Parameters.AddWithValue(proposal.ChangeId);
        command.Parameters.AddWithValue(proposal.TargetActorId);
        command.Parameters.AddWithValue(proposal.ExpectedEntitlementRevision);
        command.Parameters.AddWithValue(proposal.Actions.ToArray());
        command.Parameters.AddWithValue(proposal.CaseIds.ToArray());
        command.Parameters.AddWithValue(proposal.ProposerActorId);
        command.Parameters.AddWithValue(proposal.ProposedAtUtc);
        command.Parameters.AddWithValue(proposal.Reason);
    }

    private static DateTimeOffset Utc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
