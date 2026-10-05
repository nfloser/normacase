using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Outbound;
using NormaCase.Serialization;

namespace NormaCase.Persistence.PostgreSql;

// The durable command is the authorization boundary for non-transactional file delivery.
// Revocation blocks new commands; it cannot retract a command already committed.
public sealed class PostgresAuthorizedOutboundRequestStore(NpgsqlDataSource source)
{
    public async Task RegisterAsync(OutboundDeliveryRequest request, DateTimeOffset authorizedAtUtc,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (authorizedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC required.", nameof(authorizedAtUtc));
        var context = PostgresAuthorizedOperation.Current
            ?? throw new InvalidOperationException("Verified authorized transaction required.");
        if (!ReferenceEquals(source, context.Source))
            throw new InvalidOperationException("Authorized outbound cannot cross data sources.");
        if (!context.State.Actions.Contains("READ", StringComparer.Ordinal)
            || !context.State.Actions.Contains("EXPORT", StringComparer.Ordinal)
            || !context.State.CaseIds.Contains(request.Result.CaseId, StringComparer.Ordinal))
            throw new OutboundResultBindingException();
        var json = ReviewedCaseResultJson.Serialize(request.Result);
        var hash = PostgresBatchReviewRequestStore.Hash(json);
        try
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO normacase.authorized_outbound_requests
                    (delivery_id,destination_id,actor_id,entitlement_revision,authorized_at_utc,result_json,result_sha256)
                VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING;
                """, context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(request.Key.DeliveryId);
            insert.Parameters.AddWithValue(request.Key.DestinationId);
            insert.Parameters.AddWithValue(context.State.ActorId);
            insert.Parameters.AddWithValue(context.State.Revision);
            insert.Parameters.AddWithValue(authorizedAtUtc.UtcDateTime);
            insert.Parameters.AddWithValue(NpgsqlDbType.Json, json);
            insert.Parameters.AddWithValue(hash);
            await insert.ExecuteNonQueryAsync(token);
            await using var read = new NpgsqlCommand("""
                SELECT result_json::text,result_sha256 FROM normacase.authorized_outbound_requests
                WHERE delivery_id=$1 AND destination_id=$2;
                """, context.Connection, context.Transaction);
            read.Parameters.AddWithValue(request.Key.DeliveryId);
            read.Parameters.AddWithValue(request.Key.DestinationId);
            await using var reader = await read.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || reader.GetString(0) != json || reader.GetString(1) != hash)
                throw new OutboundDeliveryConflictException();
        }
        catch (NpgsqlException) { throw new OutboundDeliveryStorageException(); }
    }
}
