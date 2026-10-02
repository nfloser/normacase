using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Audit;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresAssessmentAuditTrailStoreTests
{
    [Fact]
    public async Task Creation_reviews_and_override_roundtrip_exactly()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "roundtrip");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var trail = CreatedTrail(record, "system-synthetic");

        await store.AppendAsync(trail);

        var accepted = new HumanReviewRecord(
            NewReviewId("accepted"),
            record.AssessmentId,
            "reviewer-one",
            record.RecordedAtUtc
                .AddMinutes(1)
                .AddTicks(3),
            HumanReviewDisposition.AcceptSystemResult,
            "Synthetische Bestätigung",
            reference: new ReviewReference(
                "synthetic",
                "reference-one"));
        trail = trail.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                accepted));
        await store.AppendAsync(trail);

        var overridden = new HumanReviewRecord(
            NewReviewId("override"),
            record.AssessmentId,
            "reviewer-two",
            record.RecordedAtUtc
                .AddMinutes(2)
                .AddTicks(9),
            HumanReviewDisposition.Override,
            "Synthetische Abweichung",
            AssessmentOutcome.Incomplete,
            new ReviewReference(
                "synthetic",
                "reference-two"));
        trail = trail.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                3,
                overridden));
        await store.AppendAsync(trail);

        var loaded = await store.LoadLatestAsync(
            record.AssessmentId);

        Assert.NotNull(loaded);
        Assert.Equal(
            AssessmentAuditJson.Serialize(trail),
            AssessmentAuditJson.Serialize(loaded!));
        Assert.Equal(
            overridden.RecordedAt.Ticks,
            loaded!.Events[^1].OccurredAt.Ticks);
        Assert.Equal(
            "Synthetische Abweichung",
            loaded.Events[^1].Review!.Reason);
        Assert.Equal(
            "reference-two",
            loaded.Events[^1].Review!.Reference!.Value);
    }

    [Fact]
    public async Task Audit_history_requires_an_existing_assessment_record()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var assessmentId = NewAssessmentId("missing");
        var trail = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                assessmentId,
                UtcTime().AddTicks(7),
                "system-synthetic"));
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);

        await Assert.ThrowsAsync<
            AssessmentAuditTrailAssessmentMissingException>(
            () => store.AppendAsync(trail));
    }

    [Fact]
    public async Task Append_must_add_exactly_one_event()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "skip");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var created = CreatedTrail(record, "system-synthetic");
        await store.AppendAsync(created);

        var second = created.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                Review(
                    record,
                    "second",
                    "reviewer",
                    1,
                    HumanReviewDisposition.AcceptSystemResult)));
        var third = second.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                3,
                Review(
                    record,
                    "third",
                    "reviewer",
                    2,
                    HumanReviewDisposition.Override,
                    AssessmentOutcome.HumanReview)));

        await Assert.ThrowsAsync<
            AssessmentAuditTrailConflictException>(
            () => store.AppendAsync(third));

        var loaded = await store.LoadLatestAsync(
            record.AssessmentId);
        Assert.Single(loaded!.Events);
    }

    [Fact]
    public async Task Append_rejects_a_divergent_existing_prefix()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "divergent");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var created = CreatedTrail(record, "system-synthetic");
        await store.AppendAsync(created);

        var storedSecond = created.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                Review(
                    record,
                    "stored",
                    "reviewer-one",
                    1,
                    HumanReviewDisposition.AcceptSystemResult)));
        await store.AppendAsync(storedSecond);

        var alternateSecond = created.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                Review(
                    record,
                    "alternate",
                    "reviewer-two",
                    1,
                    HumanReviewDisposition.AcceptSystemResult)));
        var divergentThird = alternateSecond.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                3,
                Review(
                    record,
                    "third",
                    "reviewer-two",
                    2,
                    HumanReviewDisposition.Override,
                    AssessmentOutcome.Incomplete)));

        await Assert.ThrowsAsync<
            AssessmentAuditTrailConflictException>(
            () => store.AppendAsync(divergentThird));

        var loaded = await store.LoadLatestAsync(
            record.AssessmentId);
        Assert.Equal(
            AssessmentAuditJson.Serialize(storedSecond),
            AssessmentAuditJson.Serialize(loaded!));
    }

    [Fact]
    public async Task Concurrent_competing_review_append_cannot_fork_history()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "concurrent");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var created = CreatedTrail(record, "system-synthetic");
        await store.AppendAsync(created);

        var first = created.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                Review(
                    record,
                    "first",
                    "reviewer-one",
                    1,
                    HumanReviewDisposition.AcceptSystemResult)));
        var second = created.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                2,
                Review(
                    record,
                    "second",
                    "reviewer-two",
                    1,
                    HumanReviewDisposition.Override,
                    AssessmentOutcome.Incomplete)));

        var attempts = await Task.WhenAll(
            Capture(() => store.AppendAsync(first)),
            Capture(() => store.AppendAsync(second)));

        Assert.Equal(
            1,
            attempts.Count(error => error is null));
        Assert.Equal(
            1,
            attempts.Count(
                error => error
                    is AssessmentAuditTrailConflictException));

        var loaded = await store.LoadLatestAsync(
            record.AssessmentId);
        Assert.Equal(2, loaded!.Events.Count);
        Assert.Contains(
            loaded.Events[1].Review!.ReviewId,
            new[]
            {
                first.Events[1].Review!.ReviewId,
                second.Events[1].Review!.ReviewId
            });
    }

    [Fact]
    public async Task Database_rejects_update_and_delete_of_audit_versions()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "immutable");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var trail = CreatedTrail(record, "system-synthetic");
        await store.AppendAsync(trail);

        await using var connection =
            await dataSource.OpenConnectionAsync();

        await using var update = new NpgsqlCommand(
            """
            UPDATE normacase.assessment_audit_trail_versions
            SET audit_format_version = 99
            WHERE assessment_id = $1
              AND last_sequence = 1;
            """,
            connection);
        update.Parameters.AddWithValue(
            record.AssessmentId.Value);
        var updateError =
            await Assert.ThrowsAsync<PostgresException>(
                () => update.ExecuteNonQueryAsync());
        Assert.Equal("55000", updateError.SqlState);

        await using var delete = new NpgsqlCommand(
            """
            DELETE FROM normacase.assessment_audit_trail_versions
            WHERE assessment_id = $1
              AND last_sequence = 1;
            """,
            connection);
        delete.Parameters.AddWithValue(
            record.AssessmentId.Value);
        var deleteError =
            await Assert.ThrowsAsync<PostgresException>(
                () => delete.ExecuteNonQueryAsync());
        Assert.Equal("55000", deleteError.SqlState);

        Assert.NotNull(
            await store.LoadLatestAsync(
                record.AssessmentId));
    }

    [Fact]
    public async Task Corrupt_fingerprint_fails_closed_without_review_text()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "corrupt");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var trail = CreatedTrail(record, "system-synthetic")
            .Append(
                AssessmentAuditEvent.HumanReviewRecorded(
                    2,
                    new HumanReviewRecord(
                        NewReviewId("secret"),
                        record.AssessmentId,
                        "reviewer-secret",
                        record.RecordedAtUtc.AddMinutes(1),
                        HumanReviewDisposition.AcceptSystemResult,
                        "sensitive-synthetic-review-reason",
                        reference: new ReviewReference(
                            "synthetic",
                            "sensitive-synthetic-reference"))));
        await store.AppendAsync(CreatedTrail(
            record,
            "system-synthetic"));
        await store.AppendAsync(trail);

        await using (var connection =
            await dataSource.OpenConnectionAsync())
        {
            await WithAuditUpdateTriggerDisabled(
                connection,
                async () =>
                {
                    await using var corrupt = new NpgsqlCommand(
                        """
                        UPDATE normacase.assessment_audit_trail_versions
                        SET audit_sha256 =
                            '0000000000000000000000000000000000000000000000000000000000000000'
                        WHERE assessment_id = $1
                          AND last_sequence = 2;
                        """,
                        connection);
                    corrupt.Parameters.AddWithValue(
                        record.AssessmentId.Value);
                    await corrupt.ExecuteNonQueryAsync();
                });
        }

        var exception =
            await Assert.ThrowsAsync<
                AssessmentAuditTrailIntegrityException>(
                () => store.LoadLatestAsync(
                    record.AssessmentId));

        Assert.DoesNotContain(
            "sensitive-synthetic-review-reason",
            exception.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sensitive-synthetic-reference",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corrupt_metadata_fails_closed()
    {
        await using var dataSource = CreateDataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();

        var record = await PersistAssessmentAsync(
            dataSource,
            "metadata");
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);
        var trail = CreatedTrail(record, "system-synthetic");
        await store.AppendAsync(trail);

        await using (var connection =
            await dataSource.OpenConnectionAsync())
        {
            await WithAuditUpdateTriggerDisabled(
                connection,
                async () =>
                {
                    await using var corrupt = new NpgsqlCommand(
                        """
                        UPDATE normacase.assessment_audit_trail_versions
                        SET occurred_at_utc_ticks =
                            occurred_at_utc_ticks + 1
                        WHERE assessment_id = $1
                          AND last_sequence = 1;
                        """,
                        connection);
                    corrupt.Parameters.AddWithValue(
                        record.AssessmentId.Value);
                    await corrupt.ExecuteNonQueryAsync();
                });
        }

        await Assert.ThrowsAsync<
            AssessmentAuditTrailIntegrityException>(
            () => store.LoadLatestAsync(
                record.AssessmentId));
    }

    [Fact]
    public async Task Connection_failure_does_not_echo_credentials()
    {
        const string password = "audit-synthetic-secret";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Database = "normacase",
            Username = "synthetic-audit-user",
            Password = password,
            Timeout = 1,
            CommandTimeout = 1,
            Pooling = false
        };
        await using var dataSource =
            NpgsqlDataSource.Create(builder.ConnectionString);
        var store =
            new PostgresAssessmentAuditTrailStore(dataSource);

        var exception =
            await Assert.ThrowsAsync<
                AssessmentAuditTrailStorageException>(
                () => store.LoadLatestAsync(
                    NewAssessmentId("connection")));

        Assert.DoesNotContain(
            password,
            exception.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            builder.ConnectionString,
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task WithAuditUpdateTriggerDisabled(
        NpgsqlConnection connection,
        Func<Task> action)
    {
        await using (var disable = new NpgsqlCommand(
            """
            ALTER TABLE normacase.assessment_audit_trail_versions
                DISABLE TRIGGER assessment_audit_trails_no_update;
            """,
            connection))
        {
            await disable.ExecuteNonQueryAsync();
        }

        try
        {
            await action();
        }
        finally
        {
            await using var enable = new NpgsqlCommand(
                """
                ALTER TABLE normacase.assessment_audit_trail_versions
                    ENABLE TRIGGER assessment_audit_trails_no_update;
                """,
                connection);
            await enable.ExecuteNonQueryAsync();
        }
    }

    private static async Task<AssessmentRecord>
        PersistAssessmentAsync(
            NpgsqlDataSource dataSource,
            string suffix)
    {
        var record = CreateAssessment(suffix);
        await new PostgresAssessmentRecordStore(
            dataSource).AppendAsync(record);
        return record;
    }

    private static AssessmentRecord CreateAssessment(
        string suffix)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-c-pack.json"));
        var id = NewAssessmentId(suffix);

        return new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = TruthValue.Yes,
                ["measurement"] = 15m
            },
            new DateOnly(2026, 10, 2),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = EvidenceStatus.Present
            },
            new AssessmentExecutionContext(
                id,
                "case-audit-" + suffix,
                "postgres-audit-test",
                UtcTime().AddTicks(7)));
    }

    private static AssessmentAuditTrail CreatedTrail(
        AssessmentRecord record,
        string actorId)
        => AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                record.AssessmentId,
                record.RecordedAtUtc.AddTicks(2),
                actorId));

    private static HumanReviewRecord Review(
        AssessmentRecord record,
        string suffix,
        string actor,
        int minutes,
        HumanReviewDisposition disposition,
        AssessmentOutcome? overrideOutcome = null)
        => new(
            NewReviewId(suffix),
            record.AssessmentId,
            actor,
            record.RecordedAtUtc
                .AddMinutes(minutes)
                .AddTicks(3),
            disposition,
            "Synthetischer Review " + suffix,
            overrideOutcome,
            new ReviewReference(
                "synthetic",
                "reference-" + suffix));

    private static async Task<Exception?> Capture(
        Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static AssessmentId NewAssessmentId(
        string prefix)
        => new(
            prefix
            + "-"
            + Guid.NewGuid().ToString("N"));

    private static ReviewId NewReviewId(string prefix)
        => new(
            prefix
            + "-"
            + Guid.NewGuid().ToString("N"));

    private static DateTimeOffset UtcTime()
        => new(
            2026,
            10,
            2,
            22,
            30,
            0,
            TimeSpan.Zero);

    private static NpgsqlDataSource CreateDataSource()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "NORMACASE_POSTGRES_TEST_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "NORMACASE_POSTGRES_TEST_CONNECTION is required.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }
}
