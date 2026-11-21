using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.SyntheticIntegration.Outbound;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class OutboundDeliveryTests
{
    [Fact]
    public async Task Repeated_identical_delivery_returns_original_receipt_without_second_side_effect()
    {
        var sink = new InMemoryReviewedCaseResultSink("synthetic-message-sink");
        var store = new InMemoryOutboundDeliveryReceiptStore();
        var service = new ReviewedCaseDeliveryService(store);
        var request = new OutboundDeliveryRequest("delivery-1", sink.DestinationId, Sample());

        var first = await service.DeliverAsync(request, sink);
        var second = await service.DeliverAsync(request, sink);

        Assert.Equal(OutboundDeliveryStatus.Delivered, first.Status);
        Assert.True(first.IsCommitted);
        Assert.Same(first, second);
        Assert.Single(sink.Deliveries);
        Assert.Equal("correlation-1", sink.Deliveries[0].Result.CorrelationId);
        Assert.Equal("message-1", sink.Deliveries[0].Result.MessageId);
    }

    [Fact]
    public async Task Concurrent_identical_delivery_creates_one_message_side_effect()
    {
        var sink = new InMemoryReviewedCaseResultSink("synthetic-message-sink");
        var service = new ReviewedCaseDeliveryService(new InMemoryOutboundDeliveryReceiptStore());
        var request = new OutboundDeliveryRequest("delivery-concurrent", sink.DestinationId, Sample());

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => service.DeliverAsync(request, sink)));

        Assert.All(results, item => Assert.Equal(OutboundDeliveryStatus.Delivered, item.Status));
        Assert.Single(sink.Deliveries);
        Assert.All(results, item => Assert.Equal(results[0], item));
    }

    [Fact]
    public async Task Conflicting_reuse_of_delivery_identity_fails_closed()
    {
        var sink = new InMemoryReviewedCaseResultSink("synthetic-message-sink");
        var service = new ReviewedCaseDeliveryService(new InMemoryOutboundDeliveryReceiptStore());
        var request = new OutboundDeliveryRequest("delivery-1", sink.DestinationId, Sample());

        await service.DeliverAsync(request, sink);

        var changed = request with { Result = Sample() with { CorrelationId = "correlation-2" } };
        await Assert.ThrowsAsync<OutboundDeliveryConflictException>(
            () => service.DeliverAsync(changed, sink));
        Assert.Single(sink.Deliveries);
    }

    [Fact]
    public async Task Retryable_failure_is_not_committed_and_may_be_retried_explicitly()
    {
        var sink = new InMemoryReviewedCaseResultSink(
            "synthetic-message-sink",
            [OutboundSinkDeliveryStatus.RetryableFailure, OutboundSinkDeliveryStatus.Delivered]);
        var store = new InMemoryOutboundDeliveryReceiptStore();
        var service = new ReviewedCaseDeliveryService(store);
        var request = new OutboundDeliveryRequest("delivery-1", sink.DestinationId, Sample());

        var failed = await service.DeliverAsync(request, sink);
        var delivered = await service.DeliverAsync(request, sink);

        Assert.Equal(OutboundDeliveryStatus.RetryableFailure, failed.Status);
        Assert.False(failed.IsCommitted);
        Assert.Equal(OutboundDeliveryStatus.Delivered, delivered.Status);
        Assert.True(delivered.IsCommitted);
        Assert.Equal(2, sink.Attempts);
        Assert.NotNull(await store.FindAsync(request.Key));
    }

    [Fact]
    public async Task Rejected_delivery_is_committed_and_not_replayed()
    {
        var sink = new InMemoryReviewedCaseResultSink(
            "synthetic-message-sink",
            [OutboundSinkDeliveryStatus.Rejected]);
        var service = new ReviewedCaseDeliveryService(new InMemoryOutboundDeliveryReceiptStore());
        var request = new OutboundDeliveryRequest("delivery-1", sink.DestinationId, Sample());

        var first = await service.DeliverAsync(request, sink);
        var second = await service.DeliverAsync(request, sink);

        Assert.Equal(OutboundDeliveryStatus.Rejected, first.Status);
        Assert.True(first.IsCommitted);
        Assert.Same(first, second);
        Assert.Equal(1, sink.Attempts);
    }

    [Fact]
    public async Task Cancellation_never_commits_a_receipt()
    {
        var sink = new InMemoryReviewedCaseResultSink("synthetic-message-sink", delayUntilCancelled: true);
        var store = new InMemoryOutboundDeliveryReceiptStore();
        var service = new ReviewedCaseDeliveryService(store);
        var request = new OutboundDeliveryRequest("delivery-1", sink.DestinationId, Sample());
        using var cancellation = new CancellationTokenSource();

        var delivery = service.DeliverAsync(request, sink, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery);
        Assert.Null(await store.FindAsync(request.Key));
    }

    [Fact]
    public async Task File_sink_writes_bounded_versioned_json_and_preserves_correlation()
    {
        var root = Path.Combine(Path.GetTempPath(), "normacase-outbound-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new BoundedFileReviewedCaseResultSink("synthetic-file-sink", root);
            var service = new ReviewedCaseDeliveryService(new InMemoryOutboundDeliveryReceiptStore());
            var request = new OutboundDeliveryRequest("delivery-file-1", sink.DestinationId, Sample());

            var receipt = await service.DeliverAsync(request, sink);

            Assert.Equal(OutboundDeliveryStatus.Delivered, receipt.Status);
            Assert.True(receipt.IsCommitted);
            var path = Assert.Single(Directory.GetFiles(root, "*.json"));
            var restored = NormaCase.Serialization.ReviewedCaseResultJson.Deserialize(
                await File.ReadAllTextAsync(path));
            Assert.Equal(request.Result, restored);
            Assert.Equal("correlation-1", restored.CorrelationId);

            var duplicate = await service.DeliverAsync(request, sink);
            Assert.Same(receipt, duplicate);
            Assert.Single(Directory.GetFiles(root, "*.json"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Concurrent_file_delivery_creates_one_complete_payload()
    {
        var root = Path.Combine(Path.GetTempPath(), "normacase-outbound-concurrent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new BoundedFileReviewedCaseResultSink("synthetic-file-sink", root);
            var service = new ReviewedCaseDeliveryService(new InMemoryOutboundDeliveryReceiptStore());
            var request = new OutboundDeliveryRequest("delivery-file-concurrent", sink.DestinationId, Sample());

            var results = await Task.WhenAll(
                Enumerable.Range(0, 8).Select(_ => service.DeliverAsync(request, sink)));

            Assert.All(results, item => Assert.Equal(OutboundDeliveryStatus.Delivered, item.Status));
            var path = Assert.Single(Directory.GetFiles(root, "*.json"));
            Assert.Equal(
                request.Result,
                NormaCase.Serialization.ReviewedCaseResultJson.Deserialize(await File.ReadAllTextAsync(path)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Independent_file_sink_instances_publish_one_complete_original()
    {
        var directory = Path.Combine(Path.GetTempPath(), "synthetic-independent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var one = new BoundedFileReviewedCaseResultSink("synthetic-file", directory);
            var two = new BoundedFileReviewedCaseResultSink("synthetic-file", directory);
            var request = new OutboundDeliveryRequest("same-message", "synthetic-file", Sample());
            var deliveries = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => (i % 2 == 0 ? one : two).DeliverAsync(request)));
            Assert.All(deliveries, delivery => Assert.Equal(deliveries[0], delivery));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Equal(Sample(), NormaCase.Serialization.ReviewedCaseResultJson.Deserialize(await File.ReadAllTextAsync(Directory.GetFiles(directory).Single())));
            var restarted = new BoundedFileReviewedCaseResultSink("synthetic-file", directory);
            await Assert.ThrowsAsync<OutboundDeliveryConflictException>(() => restarted.DeliverAsync(request with { Result = Sample() with { CorrelationId = "conflict" } }));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static ReviewedCaseResult Sample()
        => new(
            "message-1",
            "correlation-1",
            "synthetic-source",
            "upstream-case",
            "upstream-message",
            3,
            "case-1",
            5,
            "assessment-1",
            "synthetic-platform",
            "synthetic.demo-g",
            "demo-g-2026.1",
            new DateOnly(2026, 10, 3),
            "synthetic-review",
            1,
            "accepted",
            7,
            2,
            "review-1",
            new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            HumanReviewDisposition.AcceptSystemResult,
            AssessmentOutcome.Supported,
            AssessmentOutcome.Supported);
}
