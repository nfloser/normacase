using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace NormaCase.Persistence.PostgreSql;

public sealed class BatchReviewRequestStorageException : Exception
{
    public BatchReviewRequestStorageException()
        : base("Batch review request storage failed.") { }
}

public sealed class BatchReviewRequestIntegrityException : Exception
{
    public BatchReviewRequestIntegrityException()
        : base("Stored batch review request failed verification.") { }
}

public sealed class BatchReviewRequestConflictException : Exception
{
    public BatchReviewRequestConflictException()
        : base("Batch review request identity is already bound to different content.") { }
}

public sealed record BatchReviewRequestRegistration(
    string RequestId,
    string ActorId,
    string RequestSha256,
    DateTimeOffset ProposedRecordedAtUtc);

public sealed class PostgresBatchReviewRequestStore(NpgsqlDataSource dataSource)
{
    private static readonly Regex RequestIdPattern = new(
        "\\A[A-Za-z0-9][A-Za-z0-9._@:-]*\\z", RegexOptions.CultureInvariant);
    private static readonly Regex HashPattern = new(
        "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);
    private readonly NpgsqlDataSource source =
        dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async Task<PostgresBatchReviewRequestLease> AcquireAsync(
        BatchReviewRequestRegistration registration,
        CancellationToken cancellationToken = default)
    {
        Validate(registration);
        NpgsqlConnection? connection = null;
        try
        {
            connection = await source.OpenConnectionAsync(cancellationToken);
            await LockAsync(connection, registration.RequestId, cancellationToken);
            var existing = await ReadAsync(connection, registration.RequestId, cancellationToken);
            if (existing is null)
            {
                await InsertAsync(connection, registration, cancellationToken);
                return new(connection, registration.RequestId,
                    registration.ProposedRecordedAtUtc, resultJson: null);
            }

            EnsureSame(existing, registration);
            return new(connection, registration.RequestId,
                existing.RecordedAtUtc, existing.ResultJson);
        }
        catch (Exception exception) when (exception is NpgsqlException)
        {
            if (connection is not null) await connection.DisposeAsync();
            throw new BatchReviewRequestStorageException();
        }
        catch
        {
            if (connection is not null) await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task LockAsync(
        NpgsqlConnection connection, string requestId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_lock(hashtextextended($1, 0));", connection);
        command.Parameters.AddWithValue("normacase.batch-review:" + requestId);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<StoredRequest?> ReadAsync(
        NpgsqlConnection connection, string requestId, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT r.actor_id, r.request_sha256, r.recorded_at_utc_ticks,
                   r.recorded_at_utc, x.result_json::text, x.result_sha256
            FROM normacase.batch_review_requests r
            LEFT JOIN normacase.batch_review_results x ON x.request_id = r.request_id
            WHERE r.request_id = $1;
            """, connection);
        command.Parameters.AddWithValue(requestId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;

        var ticks = reader.GetInt64(2);
        var timestamp = reader.GetDateTime(3);
        if (ticks <= 0 || timestamp.Ticks != ticks - ticks % 10)
            throw new BatchReviewRequestIntegrityException();
        string? result = null;
        if (!reader.IsDBNull(4))
        {
            result = reader.GetString(4);
            var storedHash = reader.GetString(5);
            if (!FixedEquals(storedHash, Hash(result)))
                throw new BatchReviewRequestIntegrityException();
            try { using var _ = JsonDocument.Parse(result); }
            catch (JsonException) { throw new BatchReviewRequestIntegrityException(); }
        }
        else if (!reader.IsDBNull(5)) throw new BatchReviewRequestIntegrityException();
        return new(reader.GetString(0), reader.GetString(1),
            new DateTimeOffset(ticks, TimeSpan.Zero), result);
    }

    private static async Task InsertAsync(
        NpgsqlConnection connection,
        BatchReviewRequestRegistration registration,
        CancellationToken token)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO normacase.batch_review_requests(
                request_id, actor_id, request_sha256,
                recorded_at_utc_ticks, recorded_at_utc)
            VALUES ($1, $2, $3, $4, $5);
            """, connection);
        command.Parameters.AddWithValue(registration.RequestId);
        command.Parameters.AddWithValue(registration.ActorId);
        command.Parameters.AddWithValue(registration.RequestSha256);
        command.Parameters.AddWithValue(registration.ProposedRecordedAtUtc.Ticks);
        command.Parameters.AddWithValue(registration.ProposedRecordedAtUtc.UtcDateTime);
        await command.ExecuteNonQueryAsync(token);
    }

    private static void EnsureSame(
        StoredRequest existing, BatchReviewRequestRegistration requested)
    {
        if (!string.Equals(existing.ActorId, requested.ActorId, StringComparison.Ordinal)
            || !FixedEquals(existing.RequestSha256, requested.RequestSha256))
            throw new BatchReviewRequestConflictException();
    }

    private static void Validate(BatchReviewRequestRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrEmpty(registration.RequestId)
            || registration.RequestId.Length > 128
            || !RequestIdPattern.IsMatch(registration.RequestId)
            || string.IsNullOrWhiteSpace(registration.ActorId)
            || registration.ActorId.Length > 128
            || registration.ActorId.Any(char.IsControl)
            || registration.RequestSha256 is null
            || !HashPattern.IsMatch(registration.RequestSha256)
            || registration.ProposedRecordedAtUtc == default
            || registration.ProposedRecordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid batch review request registration.", nameof(registration));
    }

    internal static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static bool FixedEquals(string left, string right) =>
        left.Length == right.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private sealed record StoredRequest(
        string ActorId, string RequestSha256,
        DateTimeOffset RecordedAtUtc, string? ResultJson);
}

public sealed class PostgresBatchReviewRequestLease : IAsyncDisposable
{
    private NpgsqlConnection? connection;
    internal PostgresBatchReviewRequestLease(
        NpgsqlConnection connection, string requestId,
        DateTimeOffset recordedAtUtc, string? resultJson)
    {
        this.connection = connection;
        RequestId = requestId;
        RecordedAtUtc = recordedAtUtc;
        ResultJson = resultJson;
    }

    public string RequestId { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public string? ResultJson { get; private set; }

    public async Task CommitResultAsync(
        string resultJson, CancellationToken cancellationToken = default)
    {
        if (connection is null) throw new ObjectDisposedException(nameof(PostgresBatchReviewRequestLease));
        ArgumentException.ThrowIfNullOrWhiteSpace(resultJson);
        if (Encoding.UTF8.GetByteCount(resultJson) > 1_048_576)
            throw new ArgumentException("Batch result exceeds storage limit.", nameof(resultJson));
        try { using var _ = JsonDocument.Parse(resultJson); }
        catch (JsonException exception) { throw new ArgumentException("Invalid batch result JSON.", nameof(resultJson), exception); }
        if (ResultJson is not null)
        {
            if (!string.Equals(ResultJson, resultJson, StringComparison.Ordinal))
                throw new BatchReviewRequestConflictException();
            return;
        }
        try
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO normacase.batch_review_results(request_id, result_json, result_sha256)
                VALUES ($1, $2, $3);
                """, connection);
            command.Parameters.AddWithValue(RequestId);
            command.Parameters.AddWithValue(NpgsqlDbType.Json, resultJson);
            command.Parameters.AddWithValue(PostgresBatchReviewRequestStore.Hash(resultJson));
            await command.ExecuteNonQueryAsync(cancellationToken);
            ResultJson = resultJson;
        }
        catch (NpgsqlException) { throw new BatchReviewRequestStorageException(); }
    }

    public async ValueTask DisposeAsync()
    {
        var active = Interlocked.Exchange(ref connection, null);
        if (active is null) return;
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(hashtextextended($1, 0));", active);
            command.Parameters.AddWithValue("normacase.batch-review:" + RequestId);
            await command.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException) { }
        finally { await active.DisposeAsync(); }
    }
}
