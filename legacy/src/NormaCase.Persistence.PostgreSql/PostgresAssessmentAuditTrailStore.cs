using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Audit;
using NormaCase.Domain.Audit;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresAssessmentAuditTrailStore
    : IAssessmentAuditTrailStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAssessmentAuditTrailStore(
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async Task AppendAsync(
        AssessmentAuditTrail trail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trail);

        var assessmentId = trail.AssessmentId;
        var lastEvent = trail.Events[^1];
        var json = AssessmentAuditJson.Serialize(trail);
        var checksum = Sha256(json);

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            var current = await ReadLatestAsync(
                connection,
                transaction,
                assessmentId,
                forUpdate: true,
                cancellationToken);

            if (current is null)
            {
                if (trail.Events.Count != 1
                    || lastEvent.Sequence != 1
                    || lastEvent.Kind
                        != AuditEventKind.AssessmentCreated)
                {
                    throw new AssessmentAuditTrailConflictException(
                        assessmentId);
                }
            }
            else
            {
                var currentTrail = VerifyAndDeserialize(
                    assessmentId,
                    current);

                if (trail.Events.Count
                        != currentTrail.Events.Count + 1
                    || lastEvent.Sequence
                        != current.LastSequence + 1)
                {
                    throw new AssessmentAuditTrailConflictException(
                        assessmentId);
                }

                var prefix = Prefix(
                    trail,
                    currentTrail.Events.Count);
                var prefixJson =
                    AssessmentAuditJson.Serialize(prefix);

                if (!string.Equals(
                    prefixJson,
                    current.Json,
                    StringComparison.Ordinal))
                {
                    throw new AssessmentAuditTrailConflictException(
                        assessmentId);
                }
            }

            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO normacase.assessment_audit_trail_versions (
                    assessment_id,
                    last_sequence,
                    occurred_at_utc,
                    occurred_at_utc_ticks,
                    audit_format_version,
                    audit_json,
                    audit_sha256
                )
                VALUES ($1, $2, $3, $4, $5, $6, $7);
                """,
                connection,
                transaction);
            insert.Parameters.AddWithValue(
                assessmentId.Value);
            insert.Parameters.AddWithValue(
                lastEvent.Sequence);
            insert.Parameters.AddWithValue(
                NpgsqlDbType.TimestampTz,
                lastEvent.OccurredAt.UtcDateTime);
            insert.Parameters.AddWithValue(
                lastEvent.OccurredAt.Ticks);
            insert.Parameters.AddWithValue(
                AssessmentAuditJson.CurrentFormatVersion);
            insert.Parameters.AddWithValue(
                NpgsqlDbType.Json,
                json);
            insert.Parameters.AddWithValue(checksum);

            await insert.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (AssessmentAuditTrailConflictException)
        {
            throw;
        }
        catch (AssessmentAuditTrailIntegrityException)
        {
            throw;
        }
        catch (PostgresException exception)
            when (exception.SqlState
                == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new AssessmentAuditTrailAssessmentMissingException(
                assessmentId);
        }
        catch (PostgresException exception)
            when (exception.SqlState
                == PostgresErrorCodes.UniqueViolation)
        {
            throw new AssessmentAuditTrailConflictException(
                assessmentId);
        }
        catch (NpgsqlException)
        {
            throw new AssessmentAuditTrailStorageException();
        }
    }

    public async Task<AssessmentAuditTrail?> LoadLatestAsync(
        AssessmentId assessmentId,
        CancellationToken cancellationToken = default)
    {
        if (assessmentId.IsEmpty)
        {
            throw new ArgumentException(
                "Assessment id must be explicit.",
                nameof(assessmentId));
        }

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            var stored = await ReadLatestAsync(
                connection,
                transaction: null,
                assessmentId,
                forUpdate: false,
                cancellationToken);

            return stored is null
                ? null
                : VerifyAndDeserialize(
                    assessmentId,
                    stored);
        }
        catch (AssessmentAuditTrailIntegrityException)
        {
            throw;
        }
        catch (NpgsqlException)
        {
            throw new AssessmentAuditTrailStorageException();
        }
    }

    private static async Task<StoredTrail?> ReadLatestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AssessmentId assessmentId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql =
            """
            SELECT
                last_sequence,
                occurred_at_utc,
                occurred_at_utc_ticks,
                audit_format_version,
                audit_json::text,
                audit_sha256
            FROM normacase.assessment_audit_trail_versions
            WHERE assessment_id = $1
            ORDER BY last_sequence DESC
            LIMIT 1
            """
            + (forUpdate ? " FOR UPDATE;" : ";");

        await using var command = new NpgsqlCommand(
            sql,
            connection,
            transaction);
        command.Parameters.AddWithValue(
            assessmentId.Value);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new(
            reader.GetInt64(0),
            new DateTimeOffset(
                DateTime.SpecifyKind(
                    reader.GetDateTime(1),
                    DateTimeKind.Utc)),
            reader.GetInt64(2),
            reader.GetInt32(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    private static AssessmentAuditTrail VerifyAndDeserialize(
        AssessmentId assessmentId,
        StoredTrail stored)
    {
        if (stored.FormatVersion
                != AssessmentAuditJson.CurrentFormatVersion
            || !FixedTimeHashEquals(
                stored.Checksum,
                Sha256(stored.Json)))
        {
            throw new AssessmentAuditTrailIntegrityException(
                assessmentId);
        }

        AssessmentAuditTrail trail;
        try
        {
            trail = AssessmentAuditJson.Deserialize(stored.Json);
        }
        catch (JsonException)
        {
            throw new AssessmentAuditTrailIntegrityException(
                assessmentId);
        }

        var last = trail.Events[^1];
        if (trail.AssessmentId != assessmentId
            || last.Sequence != stored.LastSequence
            || last.OccurredAt.Ticks
                != stored.OccurredAtTicks
            || TruncateToPostgresMicroseconds(
                last.OccurredAt) != stored.OccurredAt)
        {
            throw new AssessmentAuditTrailIntegrityException(
                assessmentId);
        }

        return trail;
    }

    private static AssessmentAuditTrail Prefix(
        AssessmentAuditTrail trail,
        int eventCount)
    {
        var prefix = AssessmentAuditTrail.Start(
            trail.Events[0]);

        for (var index = 1; index < eventCount; index++)
            prefix = prefix.Append(trail.Events[index]);

        return prefix;
    }

    private static bool FixedTimeHashEquals(
        string left,
        string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(
            leftBytes,
            rightBytes);
    }

    private static DateTimeOffset TruncateToPostgresMicroseconds(
        DateTimeOffset value)
    {
        var ticks = value.Ticks - (value.Ticks % 10);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static string Sha256(string json)
        => Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();

    private sealed record StoredTrail(
        long LastSequence,
        DateTimeOffset OccurredAt,
        long OccurredAtTicks,
        int FormatVersion,
        string Json,
        string Checksum);
}
