using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Outbound;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

public sealed class OutboundStorageException : Exception
{
    public OutboundStorageException() : base("Outbound storage failed.") { }
}
public sealed class OutboundIntegrityException : Exception
{
    public OutboundIntegrityException() : base("Outbound receipt failed verification.") { }
}
// The synthetic message inbox commits its side effect and receipt in the same row.
// Other transports must independently honor the delivery key (e.g. atomic file sink).
public sealed class PostgresOutboundDeliveryStore(NpgsqlDataSource dataSource)
    : IOutboundDeliveryReceiptStore, IReviewedCaseResultSink
{
    private readonly NpgsqlDataSource source = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    public string DestinationId => "synthetic-inbox";
    public async Task<OutboundSinkDeliveryResult> DeliverAsync(OutboundDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        request.Validate();
        if (request.DestinationId != DestinationId) throw new OutboundDeliveryDestinationMismatchException();
        var receipt = await CommitAsync(new(request.Key, request.Result, OutboundDeliveryStatus.Delivered, true,
            "inbox:" + request.DeliveryId), cancellationToken);
        return new(OutboundSinkDeliveryStatus.Delivered, receipt.TransportReference);
    }
    public async Task<OutboundDeliveryReceipt?> FindAsync(OutboundDeliveryKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            return await Read(connection, null, key, cancellationToken);
        }
        catch (NpgsqlException) { throw new OutboundStorageException(); }
    }
    public async Task<OutboundDeliveryReceipt> CommitAsync(OutboundDeliveryReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        new OutboundDeliveryRequest(receipt.Key.DeliveryId, receipt.Key.DestinationId, receipt.Result).Validate();
        if (!receipt.IsCommitted || receipt.Status is not (OutboundDeliveryStatus.Delivered or OutboundDeliveryStatus.Rejected)
            || receipt.TransportReference is { } reference && (string.IsNullOrWhiteSpace(reference) || reference.Length > 512 || reference.Any(char.IsControl)))
            throw new ArgumentException("Invalid terminal outbound receipt.");
        var json = ReviewedCaseResultJson.Serialize(receipt.Result);
        try
        {
            await using var connection = await source.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using (var command = new NpgsqlCommand("INSERT INTO normacase.outbound_delivery_receipts VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(delivery_id,destination_id) DO NOTHING", connection, transaction))
            {
                command.Parameters.AddWithValue(receipt.Key.DeliveryId);
                command.Parameters.AddWithValue(receipt.Key.DestinationId);
                command.Parameters.AddWithValue(NpgsqlDbType.Json, json);
                command.Parameters.AddWithValue(Hash(json));
                command.Parameters.AddWithValue(receipt.Status.ToString());
                command.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)receipt.TransportReference ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            var original = await Read(connection, transaction, receipt.Key, cancellationToken) ?? throw new OutboundIntegrityException();
            if (original.Result != receipt.Result) throw new OutboundDeliveryConflictException();
            await transaction.CommitAsync(cancellationToken);
            return original;
        }
        catch (NpgsqlException) { throw new OutboundStorageException(); }
    }
    private static async Task<OutboundDeliveryReceipt?> Read(NpgsqlConnection connection, NpgsqlTransaction? transaction, OutboundDeliveryKey key, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("SELECT result_json::text,result_sha256,status,transport_reference FROM normacase.outbound_delivery_receipts WHERE delivery_id=$1 AND destination_id=$2", connection, transaction);
        command.Parameters.AddWithValue(key.DeliveryId); command.Parameters.AddWithValue(key.DestinationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var json = reader.GetString(0);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(json)), Encoding.ASCII.GetBytes(reader.GetString(1)))) throw new OutboundIntegrityException();
        try
        {
            var result = ReviewedCaseResultJson.Deserialize(json);
            new OutboundDeliveryRequest(key.DeliveryId, key.DestinationId, result).Validate();
            var status = reader.GetString(2) switch { "Delivered" => OutboundDeliveryStatus.Delivered, "Rejected" => OutboundDeliveryStatus.Rejected, _ => throw new OutboundIntegrityException() };
            var reference = reader.IsDBNull(3) ? null : reader.GetString(3);
            if (reference is not null && (string.IsNullOrWhiteSpace(reference) || reference.Length > 512 || reference.Any(char.IsControl))) throw new OutboundIntegrityException();
            return new(key, result, status, true, reference);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException) { throw new OutboundIntegrityException(); }
    }
    private static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
