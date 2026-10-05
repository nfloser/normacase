using Npgsql;
using NormaCase.Application.Authorization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresIdentityAccessAdministrationStore(
    NpgsqlDataSource dataSource) : IIdentityAccessAdministrationStore
{
    public async Task<IdentityAccessState> LoadAsync(
        string actorId,
        CancellationToken cancellationToken = default)
    {
        ValidateActor(actorId);
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT revision, suspended
                FROM normacase.identity_access_audit
                WHERE actor_id = $1
                ORDER BY revision DESC
                LIMIT 1;
                """);
            command.Parameters.AddWithValue(actorId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken)
                ? new(actorId, reader.GetInt64(0), reader.GetBoolean(1))
                : new(actorId, 0, false);
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new IdentityAccessStorageException();
        }
    }

    public async Task<IdentityAccessState> ChangeAsync(
        IdentityAccessChange change,
        CancellationToken cancellationToken = default)
    {
        Validate(change);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var actorLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended($1, 117));",
                connection, transaction))
            {
                actorLock.Parameters.AddWithValue(change.ActorId);
                await actorLock.ExecuteNonQueryAsync(cancellationToken);
            }

            long currentRevision = 0;
            bool currentSuspended = false;
            await using (var current = new NpgsqlCommand(
                """
                SELECT revision, suspended
                FROM normacase.identity_access_audit
                WHERE actor_id = $1
                ORDER BY revision DESC
                LIMIT 1;
                """, connection, transaction))
            {
                current.Parameters.AddWithValue(change.ActorId);
                await using var reader = await current.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    currentRevision = reader.GetInt64(0);
                    currentSuspended = reader.GetBoolean(1);
                }
            }

            if (currentRevision != change.ExpectedRevision || currentSuspended == change.Suspended)
                throw new IdentityAccessConflictException();

            var revision = checked(currentRevision + 1);
            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO normacase.identity_access_audit(
                    actor_id, revision, suspended, administrator_actor_id,
                    changed_at_utc, reason)
                VALUES ($1, $2, $3, $4, $5, $6);
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue(change.ActorId);
                insert.Parameters.AddWithValue(revision);
                insert.Parameters.AddWithValue(change.Suspended);
                insert.Parameters.AddWithValue(change.AdministratorActorId);
                insert.Parameters.AddWithValue(change.ChangedAtUtc);
                insert.Parameters.AddWithValue(change.Reason);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(change.ActorId, revision, change.Suspended);
        }
        catch (IdentityAccessConflictException) { throw; }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or OverflowException)
        {
            throw new IdentityAccessStorageException();
        }
    }

    public async Task<IReadOnlyList<IdentityAccessAuditEntry>> LoadHistoryAsync(
        string actorId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ValidateActor(actorId);
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT revision, suspended, administrator_actor_id, changed_at_utc, reason
                FROM normacase.identity_access_audit
                WHERE actor_id = $1
                ORDER BY revision DESC
                LIMIT $2;
                """);
            command.Parameters.AddWithValue(actorId);
            command.Parameters.AddWithValue(limit);
            var result = new List<IdentityAccessAuditEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(new(actorId, reader.GetInt64(0), reader.GetBoolean(1),
                    reader.GetString(2),
                    new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
                    reader.GetString(4)));
            return result;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            throw new IdentityAccessStorageException();
        }
    }

    private static void Validate(IdentityAccessChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ValidateActor(change.ActorId);
        ValidateActor(change.AdministratorActorId);
        if (change.ExpectedRevision < 0
            || change.ChangedAtUtc.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(change.Reason)
            || change.Reason.Length > 1000
            || change.Reason.Any(char.IsControl))
            throw new ArgumentException("Invalid identity access change.", nameof(change));
    }

    private static void ValidateActor(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128 || actorId.Any(char.IsControl))
            throw new ArgumentException("Invalid actor identity.", nameof(actorId));
    }
}
