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
        request.Validate();
        if (request.DestinationId != DestinationId) throw new OutboundDeliveryDestinationMismatchException();
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

            var temporary = Path.Combine(rootDirectory, ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, path, overwrite: false); }
                catch (IOException) when (File.Exists(path))
                {
                    await EnsureSameExistingPayload(path, request.Result, cancellationToken).ConfigureAwait(false);
                }
                return new(OutboundSinkDeliveryStatus.Delivered, "file:" + fileName);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
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
        if (new FileInfo(path).Length > MaximumPayloadBytes) throw new OutboundDeliveryConflictException();
        var existing = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken)
            .ConfigureAwait(false);
        ReviewedCaseResult restored;
        try
        {
            restored = ReviewedCaseResultJson.Deserialize(existing);
        }
        catch (System.Text.Json.JsonException)
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
