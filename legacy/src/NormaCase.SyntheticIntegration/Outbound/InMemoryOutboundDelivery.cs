using System.Collections.Concurrent;
using NormaCase.Application.Outbound;

namespace NormaCase.SyntheticIntegration.Outbound;

public sealed class InMemoryOutboundDeliveryReceiptStore : IOutboundDeliveryReceiptStore
{
    private readonly ConcurrentDictionary<OutboundDeliveryKey, OutboundDeliveryReceipt> receipts = new();

    public Task<OutboundDeliveryReceipt?> FindAsync(
        OutboundDeliveryKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        receipts.TryGetValue(key, out var receipt);
        return Task.FromResult(receipt);
    }

    public Task<OutboundDeliveryReceipt> CommitAsync(
        OutboundDeliveryReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();
        if (!receipt.IsCommitted || receipt.Status == OutboundDeliveryStatus.RetryableFailure)
            throw new ArgumentException("Only terminal delivery receipts may be committed.", nameof(receipt));

        var committed = receipts.GetOrAdd(receipt.Key, receipt);
        if (committed.Result != receipt.Result)
            throw new OutboundDeliveryConflictException();
        return Task.FromResult(committed);
    }
}

public sealed class InMemoryReviewedCaseResultSink : IReviewedCaseResultSink
{
    private readonly object gate = new();
    private readonly Queue<OutboundSinkDeliveryStatus> scriptedStatuses;
    private readonly Dictionary<string, OutboundDeliveryRequest> delivered = new(StringComparer.Ordinal);
    private readonly List<OutboundDeliveryRequest> deliveries = [];
    private readonly bool delayUntilCancelled;
    private int attempts;

    public InMemoryReviewedCaseResultSink(
        string destinationId,
        IEnumerable<OutboundSinkDeliveryStatus>? scriptedStatuses = null,
        bool delayUntilCancelled = false)
    {
        DestinationId = destinationId;
        this.scriptedStatuses = new Queue<OutboundSinkDeliveryStatus>(
            scriptedStatuses ?? []);
        this.delayUntilCancelled = delayUntilCancelled;
    }

    public string DestinationId { get; }
    public int Attempts => Volatile.Read(ref attempts);

    public IReadOnlyList<OutboundDeliveryRequest> Deliveries
    {
        get
        {
            lock (gate)
                return deliveries.ToArray();
        }
    }

    public async Task<OutboundSinkDeliveryResult> DeliverAsync(
        OutboundDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (delayUntilCancelled)
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);

        Interlocked.Increment(ref attempts);
        OutboundSinkDeliveryStatus status;
        lock (gate)
            status = scriptedStatuses.Count > 0
                ? scriptedStatuses.Dequeue()
                : OutboundSinkDeliveryStatus.Delivered;

        if (status != OutboundSinkDeliveryStatus.Delivered)
            return new(status);

        lock (gate)
        {
            if (delivered.TryGetValue(request.DeliveryId, out var existing))
            {
                if (existing.DestinationId != request.DestinationId || existing.Result != request.Result)
                    throw new OutboundDeliveryConflictException();
                return new(OutboundSinkDeliveryStatus.Delivered, "memory:" + request.DeliveryId);
            }

            delivered.Add(request.DeliveryId, request);
            deliveries.Add(request);
        }

        return new(OutboundSinkDeliveryStatus.Delivered, "memory:" + request.DeliveryId);
    }
}
