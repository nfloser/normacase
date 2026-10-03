using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class PostgresAssessmentRecordStore
    : IAssessmentRecordStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAssessmentRecordStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public async Task AppendAsync(
        AssessmentRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var json = AssessmentRecordJson.Serialize(record);
        var checksum = Sha256(json);

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO normacase.assessment_records (
                    assessment_id,
                    case_id,
                    knowledge_pack_id,
                    knowledge_release,
                    platform_version,
                    assessment_date,
                    recorded_at_utc,
                    recorded_at_utc_ticks,
                    record_format_version,
                    record_json,
                    record_sha256
                )
                VALUES (
                    $1, $2, $3, $4, $5,
                    $6, $7, $8, $9, $10, $11
                );
                """,
                connection);

            command.Parameters.AddWithValue(record.AssessmentId.Value);
            command.Parameters.AddWithValue(record.CaseId.Value);
            command.Parameters.AddWithValue(record.KnowledgePackId);
            command.Parameters.AddWithValue(record.Result.KnowledgeRelease);
            command.Parameters.AddWithValue(record.PlatformVersion);
            command.Parameters.AddWithValue(
                NpgsqlDbType.Date,
                record.Result.AssessmentDate);
            command.Parameters.AddWithValue(
                NpgsqlDbType.TimestampTz,
                record.RecordedAtUtc.UtcDateTime);
            command.Parameters.AddWithValue(
                record.RecordedAtUtc.Ticks);
            command.Parameters.AddWithValue(
                AssessmentRecordJson.CurrentFormatVersion);
            command.Parameters.AddWithValue(
                NpgsqlDbType.Json,
                json);
            command.Parameters.AddWithValue(checksum);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState
                == PostgresErrorCodes.UniqueViolation)
        {
            throw new AssessmentRecordAlreadyExistsException(
                record.AssessmentId);
        }
        catch (NpgsqlException)
        {
            throw new AssessmentRecordStorageException();
        }
    }

    public async Task<AssessmentRecord?> LoadAsync(
        AssessmentId assessmentId,
        CancellationToken cancellationToken = default)
    {
        if (assessmentId.IsEmpty)
            throw new ArgumentException(
                "Assessment id must be explicit.",
                nameof(assessmentId));

        try
        {
            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                """
                SELECT
                    case_id,
                    knowledge_pack_id,
                    knowledge_release,
                    platform_version,
                    assessment_date,
                    recorded_at_utc,
                    recorded_at_utc_ticks,
                    record_format_version,
                    record_json::text,
                    record_sha256
                FROM normacase.assessment_records
                WHERE assessment_id = $1;
                """,
                connection);
            command.Parameters.AddWithValue(assessmentId.Value);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var caseId = new CaseId(reader.GetString(0));
            var knowledgePackId = reader.GetString(1);
            var knowledgeRelease = reader.GetString(2);
            var platformVersion = reader.GetString(3);
            var assessmentDate = reader.GetFieldValue<DateOnly>(4);
            var recordedAt = new DateTimeOffset(
                DateTime.SpecifyKind(
                    reader.GetDateTime(5),
                    DateTimeKind.Utc));
            var recordedAtTicks = reader.GetInt64(6);
            var formatVersion = reader.GetInt32(7);
            var json = reader.GetString(8);
            var checksum = reader.GetString(9);

            if (formatVersion
                    != AssessmentRecordJson.CurrentFormatVersion
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(checksum),
                    Encoding.ASCII.GetBytes(Sha256(json))))
            {
                throw new AssessmentRecordIntegrityException(
                    assessmentId);
            }

            AssessmentRecord record;
            try
            {
                record = AssessmentRecordJson.Deserialize(json);
            }
            catch (JsonException)
            {
                throw new AssessmentRecordIntegrityException(
                    assessmentId);
            }

            if (record.AssessmentId != assessmentId
                || record.CaseId != caseId
                || !string.Equals(
                    record.KnowledgePackId,
                    knowledgePackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.Result.KnowledgeRelease,
                    knowledgeRelease,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.PlatformVersion,
                    platformVersion,
                    StringComparison.Ordinal)
                || record.Result.AssessmentDate != assessmentDate
                || record.RecordedAtUtc.Ticks != recordedAtTicks
                || TruncateToPostgresMicroseconds(
                    record.RecordedAtUtc) != recordedAt)
            {
                throw new AssessmentRecordIntegrityException(
                    assessmentId);
            }

            return record;
        }
        catch (AssessmentRecordIntegrityException)
        {
            throw;
        }
        catch (NpgsqlException)
        {
            throw new AssessmentRecordStorageException();
        }
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
}
