using Npgsql;
using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.Persistence.PostgreSql;
using NormaCase.SyntheticIntegration.Outbound;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresOutboundDeliveryReceiptStoreTests
{
    [Fact]
    public async Task Delivered_receipt_survives_store_restart_and_prevents_second_sink_side_effect()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();

        var deliveryId = "delivery-" + Guid.NewGuid().ToString("N");
        var destinationId = "synthetic-message-sink";
        var request = new OutboundDeliveryRequest(
            deliveryId,
            destinationId,
            Sample(deliveryId));

        var firstSink = new InMemoryReviewedCaseResultSink(destinationId);
        var firstService = new ReviewedCaseDeliveryService(
            new PostgresOutboundDeliveryReceiptStore(source));

        var first = await firstService.DeliverAsync(request, firstSink);

        Assert.Equal(OutboundDeliveryStatus.Delivered, first.Status);
        Assert.True(first.IsCommitted);
        Assert.Single(firstSink.Deliveries);

        var restartedSink = new InMemoryReviewedCaseResultSink(destinationId);
        var restartedService = new ReviewedCaseDeliveryService(
            new PostgresOutboundDeliveryReceiptStore(source));

        var replayed = await restartedService.DeliverAsync(
            request,
            restartedSink);

        Assert.Equal(first, replayed);
        Assert.Empty(restartedSink.Deliveries);
    }

    [Fact]
    public async Task Conflicting_receipt_reuse_is_rejected_after_restart()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();

        var deliveryId = "conflict-" + Guid.NewGuid().ToString("N");
        var key = new OutboundDeliveryKey(
            deliveryId,
            "synthetic-message-sink");
        var original = new OutboundDeliveryReceipt(
            key,
            Sample(deliveryId),
            OutboundDeliveryStatus.Delivered,
            IsCommitted: true,
            "transport-1");

        await new PostgresOutboundDeliveryReceiptStore(source)
            .CommitAsync(original);

        var changed = original with
        {
            Result = original.Result with { CorrelationId = "changed-correlation" }
        };

        await Assert.ThrowsAsync<OutboundDeliveryConflictException>(
            () => new PostgresOutboundDeliveryReceiptStore(source)
                .CommitAsync(changed));
    }

    [Fact]
    public async Task Concurrent_identical_commits_resolve_to_one_immutable_receipt()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();

        var deliveryId = "concurrent-" + Guid.NewGuid().ToString("N");
        var receipt = new OutboundDeliveryReceipt(
            new OutboundDeliveryKey(
                deliveryId,
                "synthetic-message-sink"),
            Sample(deliveryId),
            OutboundDeliveryStatus.Rejected,
            IsCommitted: true,
            "synthetic-rejection");

        var stores = Enumerable.Range(0, 6)
            .Select(_ => new PostgresOutboundDeliveryReceiptStore(source))
            .ToArray();

        var committed = await Task.WhenAll(
            stores.Select(store => store.CommitAsync(receipt)));

        Assert.All(committed, item => Assert.Equal(receipt, item));

        await using var connection = await source.OpenConnectionAsync();
        await using var count = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM normacase.outbound_delivery_receipts
            WHERE delivery_id=$1 AND destination_id=$2;
            """,
            connection);
        count.Parameters.AddWithValue(deliveryId);
        count.Parameters.AddWithValue("synthetic-message-sink");
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Receipt_rows_are_append_only_at_database_boundary()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();

        var deliveryId = "immutable-" + Guid.NewGuid().ToString("N");
        var receipt = new OutboundDeliveryReceipt(
            new OutboundDeliveryKey(
                deliveryId,
                "synthetic-message-sink"),
            Sample(deliveryId),
            OutboundDeliveryStatus.Delivered,
            IsCommitted: true,
            null);
        await new PostgresOutboundDeliveryReceiptStore(source)
            .CommitAsync(receipt);

        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[]
        {
            """
            UPDATE normacase.outbound_delivery_receipts
            SET status=status
            WHERE delivery_id=$1 AND destination_id=$2;
            """,
            """
            DELETE FROM normacase.outbound_delivery_receipts
            WHERE delivery_id=$1 AND destination_id=$2;
            """
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(deliveryId);
            command.Parameters.AddWithValue("synthetic-message-sink");
            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }
    }

    private static ReviewedCaseResult Sample(string suffix) =>
        new(
            "message-" + suffix,
            "correlation-" + suffix,
            "synthetic-source",
            "upstream-case",
            "upstream-message",
            3,
            "case-" + suffix,
            5,
            "assessment-" + suffix,
            "synthetic-platform",
            "synthetic.demo-g",
            "demo-g-2026.1",
            new DateOnly(2026, 10, 3),
            "synthetic-review",
            1,
            "accepted",
            7,
            2,
            "review-" + suffix,
            new DateTimeOffset(
                2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            HumanReviewDisposition.AcceptSystemResult,
            AssessmentOutcome.Supported,
            AssessmentOutcome.Supported);

    private static NpgsqlDataSource Source() =>
        NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable(
                "NORMACASE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Synthetic PostgreSQL test configuration required."));
}
