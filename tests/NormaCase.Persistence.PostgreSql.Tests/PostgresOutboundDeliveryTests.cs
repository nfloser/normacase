using Npgsql;
using NormaCase.Application.Outbound;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using NormaCase.Persistence.PostgreSql;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresOutboundDeliveryTests
{
    [Fact]
    public async Task Concurrent_delivery_commits_one_original_inbox_receipt_and_rejects_conflicts()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var store = new PostgresOutboundDeliveryStore(source);
        var id = "outbound-" + Guid.NewGuid().ToString("N");
        var result = new ReviewedCaseResult(id, "synthetic-correlation", "synthetic-source", "upstream-case", "input-message", 1,
            "case", 1, "assessment", "synthetic-platform", "synthetic.demo-g", "demo-g-2026.1", new(2026, 10, 3),
            "synthetic-workflow", 1, "accepted", 2, 2, "review", new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
            HumanReviewDisposition.AcceptSystemResult, AssessmentOutcome.Supported, AssessmentOutcome.Supported);
        var request = new OutboundDeliveryRequest(id, store.DestinationId, result);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => new ReviewedCaseDeliveryService(store).DeliverAsync(request, store)));
        Assert.All(receipts, receipt => Assert.Equal(receipts[0], receipt));
        var restored = new PostgresOutboundDeliveryStore(source);
        Assert.Equal(receipts[0], await restored.FindAsync(request.Key));
        await Assert.ThrowsAsync<OutboundDeliveryConflictException>(() => new ReviewedCaseDeliveryService(restored)
            .DeliverAsync(request with { Result = result with { CorrelationId = "changed" } }, restored));
        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[] { "DELETE FROM normacase.outbound_delivery_receipts WHERE delivery_id=$1", "UPDATE normacase.outbound_delivery_receipts SET result_sha256=result_sha256 WHERE delivery_id=$1" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(id);
            Assert.Equal("55000", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => restored.FindAsync(request.Key, cancelled.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => restored.CommitAsync(receipts[0] with { Status = OutboundDeliveryStatus.RetryableFailure, IsCommitted = false }));
    }
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION") ?? throw new InvalidOperationException("Dedicated synthetic PostgreSQL configuration required."));
}
