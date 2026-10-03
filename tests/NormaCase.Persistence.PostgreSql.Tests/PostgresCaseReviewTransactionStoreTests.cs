using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using NormaCase.Application.Assessments;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresCaseReviewTransactionStoreTests
{
    private static readonly DateTimeOffset RecordedAt =
        new DateTimeOffset(2026, 10, 3, 15, 0, 0, TimeSpan.Zero)
            .AddTicks(7);

    [Fact]
    public async Task Migration_initialization_and_load_preserve_exact_process_graph_and_audit()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);

        var loaded = await fixture.Store.LoadLatestAsync(
            fixture.Assessment.CaseId);

        Assert.NotNull(loaded);
        Assert.Equal(
            AssessmentRecordJson.Serialize(fixture.Assessment),
            AssessmentRecordJson.Serialize(loaded!.Assessment));
        Assert.Equal(4, loaded.AssessmentCaseRevision);
        Assert.Equal("pending", loaded.Process.StateId);
        Assert.Equal(0, loaded.Process.Revision);
        Assert.Equal(
            AssessmentAuditJson.Serialize(fixture.Audit),
            AssessmentAuditJson.Serialize(loaded.Audit));

        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var migration = new NpgsqlCommand(
            "SELECT count(*) FROM normacase.schema_migrations WHERE version = 4",
            connection);
        Assert.Equal(1L, await migration.ExecuteScalarAsync());

        await Assert.ThrowsAsync<CaseReviewAggregateConflictException>(
            () => fixture.Store.InitializeAsync(
                fixture.Assessment,
                4,
                fixture.Process,
                fixture.Workflow,
                fixture.Audit));
    }

    [Theory]
    [InlineData(HumanReviewDisposition.AcceptSystemResult, "accepted")]
    [InlineData(HumanReviewDisposition.Override, "corrected")]
    public async Task Real_transaction_commits_review_process_and_audit_atomically(
        HumanReviewDisposition disposition,
        string expectedState)
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);
        var service = new CaseReviewService(
            fixture.Store,
            new AllowAuthorizer());

        var result = await service.ReviewAsync(
            new AuthenticatedReviewActor(
                "synthetic-reviewer",
                "synthetic-test-authority"),
            Command(
                fixture,
                disposition,
                "review-" + Guid.NewGuid().ToString("N")),
            fixture.Workflow,
            fixture.Policy);

        Assert.Equal(expectedState, result.Process.StateId);
        Assert.Equal(1, result.Process.Revision);
        Assert.Equal(2, result.Audit.Events.Count);
        Assert.Equal(
            AssessmentOutcome.Supported,
            result.Assessment.Result.Outcome);

        var loaded = await fixture.Store.LoadLatestAsync(
            fixture.Assessment.CaseId);
        Assert.NotNull(loaded);
        Assert.Equal(expectedState, loaded!.Process.StateId);
        Assert.Equal(2, loaded.Audit.Events.Count);
        Assert.Equal(
            AssessmentRecordJson.Serialize(fixture.Assessment),
            AssessmentRecordJson.Serialize(loaded.Assessment));
    }

    [Fact]
    public async Task Competing_reviews_produce_one_winner_and_one_stale_loser()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);
        var service = new CaseReviewService(
            fixture.Store,
            new AllowAuthorizer());
        var actor = new AuthenticatedReviewActor(
            "synthetic-reviewer",
            "synthetic-test-authority");

        async Task<Exception?> Attempt(string reviewId)
        {
            try
            {
                await service.ReviewAsync(
                    actor,
                    Command(
                        fixture,
                        HumanReviewDisposition.AcceptSystemResult,
                        reviewId),
                    fixture.Workflow,
                    fixture.Policy);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var results = await Task.WhenAll(
            Attempt("review-race-a-" + Guid.NewGuid().ToString("N")),
            Attempt("review-race-b-" + Guid.NewGuid().ToString("N")));

        Assert.Single(results, error => error is null);
        Assert.Single(
            results,
            error => error is CaseReviewConflictException);

        var loaded = await fixture.Store.LoadLatestAsync(
            fixture.Assessment.CaseId);
        Assert.Equal(1, loaded!.Process.Revision);
        Assert.Equal(2, loaded.Audit.Events.Count);
        Assert.Equal(
            2L,
            await AggregateRows(
                dataSource,
                fixture.Assessment.CaseId));
    }

    [Fact]
    public async Task Authorization_denial_and_database_insert_failure_leave_initial_version_unchanged()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);
        var actor = new AuthenticatedReviewActor(
            "synthetic-reviewer",
            "synthetic-test-authority");

        await Assert.ThrowsAsync<CaseReviewDeniedException>(
            () => new CaseReviewService(
                    fixture.Store,
                    new DenyAuthorizer())
                .ReviewAsync(
                    actor,
                    Command(
                        fixture,
                        HumanReviewDisposition.AcceptSystemResult,
                        "review-denied-" + Guid.NewGuid().ToString("N")),
                    fixture.Workflow,
                    fixture.Policy));

        Assert.Equal(
            1L,
            await AggregateRows(
                dataSource,
                fixture.Assessment.CaseId));

        await InstallRejectingInsertTrigger(dataSource);
        try
        {
            await Assert.ThrowsAsync<CaseReviewAggregateStorageException>(
                () => new CaseReviewService(
                        fixture.Store,
                        new AllowAuthorizer())
                    .ReviewAsync(
                        actor,
                        Command(
                            fixture,
                            HumanReviewDisposition.AcceptSystemResult,
                            "review-db-failure-" + Guid.NewGuid().ToString("N")),
                        fixture.Workflow,
                        fixture.Policy));
        }
        finally
        {
            await RemoveRejectingInsertTrigger(dataSource);
        }

        var loaded = await fixture.Store.LoadLatestAsync(
            fixture.Assessment.CaseId);
        Assert.Equal("pending", loaded!.Process.StateId);
        Assert.Equal(0, loaded.Process.Revision);
        Assert.Single(loaded.Audit.Events);
        Assert.Equal(
            1L,
            await AggregateRows(
                dataSource,
                fixture.Assessment.CaseId));
    }

    [Fact]
    public async Task Database_rejects_update_and_delete_of_aggregate_versions()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);

        await using var connection =
            await dataSource.OpenConnectionAsync();
        foreach (var sql in new[]
        {
            "UPDATE normacase.case_review_aggregate_versions SET state_id = 'changed' WHERE case_id = $1",
            "DELETE FROM normacase.case_review_aggregate_versions WHERE case_id = $1"
        })
        {
            await using var command = new NpgsqlCommand(
                sql,
                connection);
            command.Parameters.AddWithValue(
                fixture.Assessment.CaseId.Value);
            var exception =
                await Assert.ThrowsAsync<PostgresException>(
                    () => command.ExecuteNonQueryAsync());
            Assert.Equal("55000", exception.SqlState);
        }

        Assert.NotNull(
            await fixture.Store.LoadLatestAsync(
                fixture.Assessment.CaseId));
    }

    [Theory]
    [InlineData("process_checksum")]
    [InlineData("audit_checksum")]
    [InlineData("state")]
    [InlineData("audit_sequence")]
    public async Task Corrupt_hash_or_indexed_metadata_fails_before_state_is_returned(
        string corruption)
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = await Initialize(dataSource);

        await using (var connection =
            await dataSource.OpenConnectionAsync())
        {
            await using var disable = new NpgsqlCommand(
                "ALTER TABLE normacase.case_review_aggregate_versions DISABLE TRIGGER case_review_aggregate_no_update;",
                connection);
            await disable.ExecuteNonQueryAsync();

            await using var corrupt = new NpgsqlCommand(
                corruption switch
                {
                    "process_checksum" =>
                        "UPDATE normacase.case_review_aggregate_versions SET process_sha256 = repeat('0',64) WHERE case_id = $1",
                    "audit_checksum" =>
                        "UPDATE normacase.case_review_aggregate_versions SET audit_sha256 = repeat('0',64) WHERE case_id = $1",
                    "state" =>
                        "UPDATE normacase.case_review_aggregate_versions SET state_id = 'substituted' WHERE case_id = $1",
                    _ =>
                        "UPDATE normacase.case_review_aggregate_versions SET audit_last_sequence = 99 WHERE case_id = $1"
                },
                connection);
            corrupt.Parameters.AddWithValue(
                fixture.Assessment.CaseId.Value);
            await corrupt.ExecuteNonQueryAsync();

            await using var enable = new NpgsqlCommand(
                "ALTER TABLE normacase.case_review_aggregate_versions ENABLE TRIGGER case_review_aggregate_no_update;",
                connection);
            await enable.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<CaseReviewAggregateIntegrityException>(
            () => fixture.Store.LoadLatestAsync(
                fixture.Assessment.CaseId));
    }

    [Fact]
    public async Task Initialization_rejects_wrong_case_revision_audit_binding_and_missing_assessment()
    {
        await using var dataSource = DataSource();
        await new PostgresMigrationRunner(dataSource).MigrateAsync();
        var fixture = CreateFixture(dataSource);

        await Assert.ThrowsAsync<CaseReviewAggregateBindingException>(
            () => fixture.Store.InitializeAsync(
                fixture.Assessment,
                5,
                fixture.Process,
                fixture.Workflow,
                fixture.Audit));

        var wrongAudit = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                fixture.Assessment.AssessmentId,
                fixture.Assessment.RecordedAtUtc.AddSeconds(1),
                "synthetic-ingest"));
        await Assert.ThrowsAsync<CaseReviewAggregateBindingException>(
            () => fixture.Store.InitializeAsync(
                fixture.Assessment,
                4,
                fixture.Process,
                fixture.Workflow,
                wrongAudit));

        await Assert.ThrowsAsync<CaseReviewAggregateBindingException>(
            () => fixture.Store.InitializeAsync(
                fixture.Assessment,
                4,
                fixture.Process,
                fixture.Workflow,
                fixture.Audit));
    }

    private static async Task<Fixture> Initialize(
        NpgsqlDataSource dataSource)
    {
        var fixture = CreateFixture(dataSource);
        await new PostgresAssessmentRecordStore(dataSource)
            .AppendAsync(fixture.Assessment);
        await fixture.Store.InitializeAsync(
            fixture.Assessment,
            4,
            fixture.Process,
            fixture.Workflow,
            fixture.Audit);
        return fixture;
    }

    private static Fixture CreateFixture(
        NpgsqlDataSource dataSource)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var caseId = new CaseId("case-review-pg-" + suffix);
        var assessment = Assessment(caseId, suffix);
        var workflow = new WorkflowDefinition(
            "synthetic-review-pg",
            1,
            "pending",
            [
                new("pending", false),
                new("accepted", true),
                new("corrected", true)
            ],
            [
                new("accept", "pending", "accepted"),
                new("correct", "pending", "corrected")
            ]);
        var process = CaseProcessingInstance.Start(
            caseId,
            4,
            workflow);
        var audit = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                assessment.AssessmentId,
                assessment.RecordedAtUtc,
                "synthetic-ingest"));
        var policy = new CaseReviewPolicy(
            "synthetic-review-pg-policy",
            1,
            workflow.Id,
            workflow.Version,
            new Dictionary<HumanReviewDisposition, string>
            {
                [HumanReviewDisposition.AcceptSystemResult] = "accept",
                [HumanReviewDisposition.Override] = "correct"
            });

        return new(
            assessment,
            workflow,
            process,
            audit,
            policy,
            new PostgresCaseReviewTransactionStore(dataSource));
    }

    private static AssessmentRecord Assessment(
        CaseId caseId,
        string suffix)
    {
        var pack = new KnowledgePackLoader().LoadFromFile(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "demo-c-pack.json"));

        return new AssessmentRecorder().Evaluate(
            pack,
            new Dictionary<string, CaseValue>
            {
                ["request_confirmed"] = TruthValue.Yes,
                ["measurement"] = 15m,
                ["alternative_confirmed"] = TruthValue.No
            },
            new DateOnly(2026, 10, 3),
            new Dictionary<string, EvidenceStatus>
            {
                ["verification"] = EvidenceStatus.Present
            },
            new AssessmentExecutionContext(
                new AssessmentId("assessment-review-pg-" + suffix),
                caseId,
                "platform-postgres-test",
                RecordedAt));
    }

    private static CaseReviewCommand Command(
        Fixture fixture,
        HumanReviewDisposition disposition,
        string reviewId)
        => new(
            fixture.Assessment.CaseId,
            fixture.Assessment.AssessmentId,
            new ReviewId(reviewId),
            ExpectedCaseRevision: 4,
            ExpectedProcessRevision: 0,
            ExpectedAuditRevision: 1,
            RecordedAt.AddMinutes(1),
            disposition,
            "Synthetische PostgreSQL Review-Begründung.",
            disposition == HumanReviewDisposition.Override
                ? AssessmentOutcome.NotSupported
                : null);

    private static async Task<long> AggregateRows(
        NpgsqlDataSource dataSource,
        CaseId caseId)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM normacase.case_review_aggregate_versions
            WHERE case_id = $1;
            """,
            connection);
        command.Parameters.AddWithValue(caseId.Value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task InstallRejectingInsertTrigger(
        NpgsqlDataSource dataSource)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE OR REPLACE FUNCTION normacase.reject_synthetic_review_insert()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW.process_revision > 0 THEN
                    RAISE EXCEPTION 'synthetic insert failure'
                        USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END;
            $$;
            DROP TRIGGER IF EXISTS synthetic_review_insert_failure
                ON normacase.case_review_aggregate_versions;
            CREATE TRIGGER synthetic_review_insert_failure
            BEFORE INSERT ON normacase.case_review_aggregate_versions
            FOR EACH ROW
            EXECUTE FUNCTION normacase.reject_synthetic_review_insert();
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RemoveRejectingInsertTrigger(
        NpgsqlDataSource dataSource)
    {
        await using var connection =
            await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            DROP TRIGGER IF EXISTS synthetic_review_insert_failure
                ON normacase.case_review_aggregate_versions;
            DROP FUNCTION IF EXISTS normacase.reject_synthetic_review_insert();
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static NpgsqlDataSource DataSource()
        => NpgsqlDataSource.Create(
            Environment.GetEnvironmentVariable(
                "NORMACASE_POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "NORMACASE_POSTGRES_TEST_CONNECTION is required."));

    private sealed record Fixture(
        AssessmentRecord Assessment,
        WorkflowDefinition Workflow,
        CaseProcessingInstance Process,
        AssessmentAuditTrail Audit,
        CaseReviewPolicy Policy,
        PostgresCaseReviewTransactionStore Store);

    private sealed class AllowAuthorizer
        : ICaseReviewAuthorizer
    {
        public bool Authorize(
            AuthenticatedReviewActor actor,
            CaseReviewState state,
            CaseReviewCommand command,
            CaseReviewPolicy policy)
            => true;
    }

    private sealed class DenyAuthorizer
        : ICaseReviewAuthorizer
    {
        public bool Authorize(
            AuthenticatedReviewActor actor,
            CaseReviewState state,
            CaseReviewCommand command,
            CaseReviewPolicy policy)
            => false;
    }
}
