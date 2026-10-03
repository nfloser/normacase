using System.Text.RegularExpressions;

namespace NormaCase.Application.Outbound;

public readonly record struct OutboundDeliveryKey(string DeliveryId, string DestinationId);

public sealed record OutboundDeliveryRequest(
    string DeliveryId,
    string DestinationId,
    ReviewedCaseResult Result)
{
    private static readonly Regex IdentifierPattern = new(
        "\\A[A-Za-z0-9][A-Za-z0-9._@-]*\\z",
        RegexOptions.CultureInvariant);

    public OutboundDeliveryKey Key => new(DeliveryId, DestinationId);

    public void Validate()
    {
        ValidateIdentifier(DeliveryId, nameof(DeliveryId));
        ValidateIdentifier(DestinationId, nameof(DestinationId));
        ArgumentNullException.ThrowIfNull(Result);
        Result.Validate();
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (value is null || value.Length > 128 || !IdentifierPattern.IsMatch(value))
            throw new ArgumentException("Invalid outbound delivery identifier.", parameterName);
    }
}

public enum OutboundSinkDeliveryStatus
{
    Delivered,
    RetryableFailure,
    Rejected
}

public sealed record OutboundSinkDeliveryResult(
    OutboundSinkDeliveryStatus Status,
    string? TransportReference = null);

public enum OutboundDeliveryStatus
{
    Delivered,
    RetryableFailure,
    Rejected
}

public sealed record OutboundDeliveryReceipt(
    OutboundDeliveryKey Key,
    ReviewedCaseResult Result,
    OutboundDeliveryStatus Status,
    bool IsCommitted,
    string? TransportReference);

public interface IReviewedCaseResultSink
{
    string DestinationId { get; }

    Task<OutboundSinkDeliveryResult> DeliverAsync(
        OutboundDeliveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface IOutboundDeliveryReceiptStore
{
    Task<OutboundDeliveryReceipt?> FindAsync(
        OutboundDeliveryKey key,
        CancellationToken cancellationToken = default);

    Task<OutboundDeliveryReceipt> CommitAsync(
        OutboundDeliveryReceipt receipt,
        CancellationToken cancellationToken = default);
}

public sealed class ReviewedCaseDeliveryService
{
    private readonly IOutboundDeliveryReceiptStore receipts;

    public ReviewedCaseDeliveryService(IOutboundDeliveryReceiptStore receipts)
    {
        this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    }

    public async Task<OutboundDeliveryReceipt> DeliverAsync(
        OutboundDeliveryRequest request,
        IReviewedCaseResultSink sink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);
        request.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(request.DestinationId, sink.DestinationId, StringComparison.Ordinal))
            throw new OutboundDeliveryDestinationMismatchException();

        var existing = await receipts.FindAsync(request.Key, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureSamePayload(existing, request);
            return existing;
        }

        var sinkResult = await sink.DeliverAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var status = sinkResult.Status switch
        {
            OutboundSinkDeliveryStatus.Delivered => OutboundDeliveryStatus.Delivered,
            OutboundSinkDeliveryStatus.RetryableFailure => OutboundDeliveryStatus.RetryableFailure,
            OutboundSinkDeliveryStatus.Rejected => OutboundDeliveryStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(sinkResult))
        };

        var terminal = status is OutboundDeliveryStatus.Delivered or OutboundDeliveryStatus.Rejected;
        var receipt = new OutboundDeliveryReceipt(
            request.Key,
            request.Result,
            status,
            terminal,
            BoundedReference(sinkResult.TransportReference));

        if (!terminal)
            return receipt;

        var committed = await receipts.CommitAsync(receipt, cancellationToken).ConfigureAwait(false);
        EnsureSamePayload(committed, request);
        return committed;
    }

    private static void EnsureSamePayload(
        OutboundDeliveryReceipt existing,
        OutboundDeliveryRequest request)
    {
        if (existing.Key != request.Key || existing.Result != request.Result)
            throw new OutboundDeliveryConflictException();
    }

    private static string? BoundedReference(string? value)
    {
        if (value is null)
            return null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl))
            throw new InvalidOperationException("Sink returned an invalid transport reference.");
        return value;
    }
}

public sealed class OutboundDeliveryConflictException : InvalidOperationException
{
    public OutboundDeliveryConflictException()
        : base("Outbound delivery identity was reused for different content.") { }
}

public sealed class OutboundDeliveryDestinationMismatchException : InvalidOperationException
{
    public OutboundDeliveryDestinationMismatchException()
        : base("Outbound delivery destination does not match the selected sink.") { }
}
