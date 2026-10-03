using System.Security.Cryptography;
using System.Text;
using NormaCase.Application.Outbound;
using NormaCase.Serialization;

namespace NormaCase.SyntheticIntegration.Outbound;

public sealed class BoundedFileReviewedCaseResultSink : IReviewedCaseResultSink
{
    public const int MaximumPayloadBytes = 64 * 1024;
    private readonly string rootDirectory;
    private readonly SemaphoreSlim gate = new(1, 1);

    public BoundedFileReviewedCaseResultSink(string destinationId, string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        DestinationId = destinationId;
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(this.rootDirectory);
    }

    public string DestinationId { get; }

    public async Task<OutboundSinkDeliveryResult> DeliverAsync(
        OutboundDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var payload = ReviewedCaseResultJson.Serialize(request.Result);
        var bytes = new UTF8Encoding(false, true).GetBytes(payload);
        if (bytes.Length > MaximumPayloadBytes)
            return new(OutboundSinkDeliveryStatus.Rejected);

        var fileName = FileName(request.DeliveryId);
        var path = Path.Combine(rootDirectory, fileName);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                await EnsureSameExistingPayload(path, request.Result, cancellationToken)
                    .ConfigureAwait(false);
                return new(OutboundSinkDeliveryStatus.Delivered, "file:" + fileName);
            }

            await using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return new(OutboundSinkDeliveryStatus.Delivered, "file:" + fileName);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task EnsureSameExistingPayload(
        string path,
        ReviewedCaseResult expected,
        CancellationToken cancellationToken)
    {
        var existing = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        ReviewedCaseResult restored;
        try
        {
            restored = ReviewedCaseResultJson.Deserialize(existing);
        }
        catch
        {
            throw new OutboundDeliveryConflictException();
        }

        if (restored != expected)
            throw new OutboundDeliveryConflictException();
    }

    private static string FileName(string deliveryId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deliveryId));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".json";
    }
}
