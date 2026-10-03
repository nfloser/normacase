using Npgsql;
using NormaCase.Application.Outbound;

namespace NormaCase.Persistence.PostgreSql;

// The synthetic message inbox is the committed delivery row itself: the side effect
// and receipt are atomic. Reuse the shared durable receipt store and migration.
public sealed class PostgresOutboundDeliveryStore(NpgsqlDataSource dataSource)
    : IOutboundDeliveryReceiptStore, IReviewedCaseResultSink
{
    private readonly PostgresOutboundDeliveryReceiptStore receipts = new(dataSource);
    public string DestinationId => "synthetic-inbox";
    public Task<OutboundDeliveryReceipt?> FindAsync(OutboundDeliveryKey key, CancellationToken cancellationToken = default)
        => receipts.FindAsync(key, cancellationToken);
    public Task<OutboundDeliveryReceipt> CommitAsync(OutboundDeliveryReceipt receipt, CancellationToken cancellationToken = default)
        => receipts.CommitAsync(receipt, cancellationToken);
    public async Task<OutboundSinkDeliveryResult> DeliverAsync(OutboundDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (request.DestinationId != DestinationId) throw new OutboundDeliveryDestinationMismatchException();
        var receipt = await receipts.CommitAsync(new(request.Key, request.Result, OutboundDeliveryStatus.Delivered, true,
            "inbox:" + request.DeliveryId), cancellationToken);
        return new(OutboundSinkDeliveryStatus.Delivered, receipt.TransportReference);
    }
}
