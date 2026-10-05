using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Outbound;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class OutboundDeliveryStorageException : Exception
{
    public OutboundDeliveryStorageException()
        : base("Outbound delivery receipt storage failed.") { }
}

public sealed class OutboundDeliveryIntegrityException : Exception
{
    public OutboundDeliveryIntegrityException()
        : base("Stored outbound delivery receipt failed verification.") { }
}

public sealed class PostgresOutboundDeliveryReceiptStore(NpgsqlDataSource dataSource)
    : IOutboundDeliveryReceiptStore
{
    private static readonly Regex IdentifierPattern = new(
        "\\A[A-Za-z0-9][A-Za-z0-9._@-]*\\z",
        RegexOptions.CultureInvariant);

    private readonly NpgsqlDataSource source =
        dataSource ?? throw new ArgumentNullException(nameof(dataSource));

    public async Task<OutboundDeliveryReceipt?> FindAsync(
        OutboundDeliveryKey key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, cancellationToken);
            var connection = session.Connection;
            return await ReadAsync(
                connection,
                transaction: null,
                key,
                cancellationToken);
        }
        catch (NpgsqlException)
        {
            throw new OutboundDeliveryStorageException();
        }
    }

    public async Task<OutboundDeliveryReceipt> CommitAsync(
        OutboundDeliveryReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ValidateReceipt(receipt);
        try
        {
            await using var session = await PostgresOperationSession.OpenAsync(source, cancellationToken);
            var connection = session.Connection;
            var transaction = session.Transaction;

            await LockAsync(
                connection,
                transaction,
                receipt.Key,
                cancellationToken);

            var existing = await ReadAsync(
                connection,
                transaction,
                receipt.Key,
                cancellationToken);
            if (existing is not null)
            {
                EnsureSame(existing, receipt);
                await session.CommitAsync(cancellationToken);
                return existing;
            }

            var json = ReviewedCaseResultJson.Serialize(receipt.Result);
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO normacase.outbound_delivery_receipts(
                    delivery_id,
                    destination_id,
                    status,
                    result_json,
                    result_sha256,
                    transport_reference)
                VALUES ($1, $2, $3, $4, $5, $6);
                """,
                connection,
                transaction);
            insert.Parameters.AddWithValue(receipt.Key.DeliveryId);
            insert.Parameters.AddWithValue(receipt.Key.DestinationId);
            insert.Parameters.AddWithValue((int)receipt.Status);
            insert.Parameters.AddWithValue(NpgsqlDbType.Json, json);
            insert.Parameters.AddWithValue(Hash(json));
            insert.Parameters.AddWithValue(
                (object?)receipt.TransportReference ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await session.CommitAsync(cancellationToken);
            return receipt;
        }
        catch (NpgsqlException)
        {
            throw new OutboundDeliveryStorageException();
        }
    }

    private static async Task<OutboundDeliveryReceipt?> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        OutboundDeliveryKey key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT status, result_json::text, result_sha256, transport_reference
            FROM normacase.outbound_delivery_receipts
            WHERE delivery_id = $1 AND destination_id = $2;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue(key.DeliveryId);
        command.Parameters.AddWithValue(key.DestinationId);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var statusValue = reader.GetInt32(0);
        var json = reader.GetString(1);
        var storedHash = reader.GetString(2);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(storedHash),
                Encoding.ASCII.GetBytes(Hash(json))))
        {
            throw new OutboundDeliveryIntegrityException();
        }

        ReviewedCaseResult result;
        try
        {
            result = ReviewedCaseResultJson.Deserialize(json);
        }
        catch (Exception exception)
            when (exception is JsonException or ArgumentException)
        {
            throw new OutboundDeliveryIntegrityException();
        }

        if (!Enum.IsDefined(typeof(OutboundDeliveryStatus), statusValue))
            throw new OutboundDeliveryIntegrityException();

        var status = (OutboundDeliveryStatus)statusValue;
        if (status is not OutboundDeliveryStatus.Delivered
            and not OutboundDeliveryStatus.Rejected)
        {
            throw new OutboundDeliveryIntegrityException();
        }

        var transportReference =
            reader.IsDBNull(3) ? null : reader.GetString(3);
        var receipt = new OutboundDeliveryReceipt(
            key,
            result,
            status,
            IsCommitted: true,
            transportReference);
        ValidateReceipt(receipt);
        return receipt;
    }

    private static async Task LockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OutboundDeliveryKey key,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1, 0));",
            connection,
            transaction);
        command.Parameters.AddWithValue(
            $"normacase.outbound:{key.DestinationId}:{key.DeliveryId}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateReceipt(OutboundDeliveryReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ValidateKey(receipt.Key);
        new OutboundDeliveryRequest(
            receipt.Key.DeliveryId,
            receipt.Key.DestinationId,
            receipt.Result).Validate();

        if (!receipt.IsCommitted
            || receipt.Status is not OutboundDeliveryStatus.Delivered
                and not OutboundDeliveryStatus.Rejected)
        {
            throw new ArgumentException(
                "Only terminal committed outbound receipts may be persisted.",
                nameof(receipt));
        }

        if (receipt.TransportReference is not null
            && (string.IsNullOrWhiteSpace(receipt.TransportReference)
                || receipt.TransportReference.Length > 512
                || receipt.TransportReference.Any(char.IsControl)))
        {
            throw new ArgumentException(
                "Invalid outbound transport reference.",
                nameof(receipt));
        }
    }

    private static void ValidateKey(OutboundDeliveryKey key)
    {
        if (key.DeliveryId is null
            || key.DeliveryId.Length > 128
            || !IdentifierPattern.IsMatch(key.DeliveryId)
            || key.DestinationId is null
            || key.DestinationId.Length > 128
            || !IdentifierPattern.IsMatch(key.DestinationId))
        {
            throw new ArgumentException("Invalid outbound delivery key.", nameof(key));
        }
    }

    private static void EnsureSame(
        OutboundDeliveryReceipt existing,
        OutboundDeliveryReceipt requested)
    {
        if (existing.Key != requested.Key
            || existing.Result != requested.Result
            || existing.Status != requested.Status
            || existing.TransportReference != requested.TransportReference)
        {
            throw new OutboundDeliveryConflictException();
        }
    }

    private static string Hash(string json) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json)))
        .ToLowerInvariant();
}
