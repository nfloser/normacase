using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Authorization;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresCaseReviewStoreTests
{
    private static readonly WorkflowDefinition Workflow = new("synthetic-pg-review", 1, "pending",
        [new("pending", false), new("accepted", true), new("corrected", true)],
        [new("accept", "pending", "accepted"), new("correct", "pending", "corrected")]);
    private static readonly CaseReviewPolicy Policy = new("synthetic-pg-policy", 1, Workflow.Id, 1,
        new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.AcceptSystemResult] = "accept", [HumanReviewDisposition.Override] = "correct" });
    private static readonly DateTimeOffset Time = new(2026, 10, 3, 14, 0, 0, TimeSpan.Zero);
    private static readonly AuthenticatedReviewActor Actor = new("synthetic-local:assessor", "synthetic-local");

    [Fact]
    public async Task Keyset_pages_filter_authorized_scope_before_limit_and_retain_order()
    {
        await using var source = Source();
        var states = new[] { await Seed(source), await Seed(source), await Seed(source) };
        var scope = states.Select(state => state.Process.CaseId).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
        var store = Store(source);
        var first = await store.ListCasePageAsync(1, null, scope);
        Assert.Equal(scope.Take(2), first);
        var next = await store.ListCasePageAsync(1, first[0].Value, scope);
        Assert.Equal(scope.Skip(1), next);
        Assert.Equal(new[] { scope[2] }, await store.ListCasePageAsync(1, next[0].Value, scope));
        Assert.Empty(await store.ListCasePageAsync(1, scope[2].Value, scope));
        Assert.Empty(await store.ListCasePageAsync(1, null, []));
        Assert.Equal(new[] { scope[2] }, await store.ListCasePageAsync(1, null, [scope[2]]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.ListCasePageAsync(101, null, scope));
    }

    [Fact]
    public async Task Terminated_authorization_transaction_cannot_commit_a_stale_review()
    {
        await using var source = Source();
        var initial = await Seed(source);
        var actor = "synthetic-local:loss-" + Guid.NewGuid().ToString("N");
        var entitlements = new PostgresReviewedEntitlementChangeStore(source);
        await entitlements.ReconcileBaselineAsync(new(actor, 0, ["READ", "ACCEPT"], [initial.Process.CaseId.Value]));
        var proposal = new EntitlementChangeProposal("loss-" + Guid.NewGuid().ToString("N"), actor, 0,
            [], [], "synthetic-local:proposer", Time, "Synthetischer Entzug");
        await entitlements.ProposeAsync(proposal);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = entitlements.ExecuteWithEffectiveLockAsync(actor, async (snapshot, token) =>
        {
            Assert.Equal(0, snapshot.Revision);
            entered.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            return await new CaseReviewService(Store(source), new Authorizer(true)).ReviewAsync(
                new(actor, "synthetic-local"), Command(initial), Workflow, Policy, token);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            await using var terminate = source.CreateCommand("""
                SELECT pg_terminate_backend(pid) FROM pg_locks
                WHERE locktype='advisory' AND objsubid=1 AND granted
                AND ((classid::bigint << 32) | objid::bigint)=hashtextextended($1,117);
                """);
            terminate.Parameters.AddWithValue(actor);
            Assert.True((bool)(await terminate.ExecuteScalarAsync())!);
            await entitlements.DecideAsync(new(proposal.ChangeId, "synthetic-local:approver",
                Time.AddMinutes(1), true, "Synthetische Gegenprüfung"), true);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<Exception>(() => operation);
        var retained = (await Store(source).LoadAsync(initial.Process.CaseId))!;
        Assert.Equal(CaseReviewStateJson.Serialize(initial), CaseReviewStateJson.Serialize(retained));
        Assert.Equal(1, (await entitlements.LoadEffectiveAsync(actor)).Revision);
    }

    [Fact]
    public async Task Authorized_operation_uses_one_pool_connection_and_rolls_back_all_nested_writes()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION"))
            { MaxPoolSize = 1, Timeout = 3 };
        await using var source = NpgsqlDataSource.Create(builder.ConnectionString);
        var initial = await Seed(source);
        var actor = "synthetic-local:atomic-" + Guid.NewGuid().ToString("N");
        var entitlements = new PostgresReviewedEntitlementChangeStore(source);
        await entitlements.ReconcileBaselineAsync(new(actor, 0, ["READ", "ACCEPT"], [initial.Process.CaseId.Value]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => entitlements.ExecuteWithEffectiveLockAsync<int>(actor,
            async (_, token) =>
            {
                await new CaseReviewService(Store(source), new Authorizer(true)).ReviewAsync(
                    new(actor, "synthetic-local"), Command(initial), Workflow, Policy, token);
                throw new InvalidOperationException("Synthetic callback failure");
            }));
        Assert.Equal(CaseReviewStateJson.Serialize(initial),
            CaseReviewStateJson.Serialize((await Store(source).LoadAsync(initial.Process.CaseId))!));
    }

    [Theory]
    [InlineData(HumanReviewDisposition.AcceptSystemResult, "accepted")]
    [InlineData(HumanReviewDisposition.Override, "corrected")]
    public async Task Review_roundtrip_preserves_exact_assessment_and_commits_audit_and_process(HumanReviewDisposition disposition, string expected)
    {
        await using var source = Source();
        var initial = await Seed(source);
        var store = Store(source);
        var committed = await new CaseReviewService(store, new Authorizer(true)).ReviewAsync(Actor, Command(initial, disposition), Workflow, Policy);
        var loaded = (await Store(source).LoadAsync(initial.Process.CaseId))!;
        Assert.Equal(CaseReviewStateJson.Serialize(committed), CaseReviewStateJson.Serialize(loaded));
        Assert.Equal(expected, loaded.Process.StateId);
        Assert.Equal(2, loaded.Audit.Events.Count);
        Assert.Equal(AssessmentRecordJson.Serialize(initial.Assessment), AssessmentRecordJson.Serialize(loaded.Assessment));
        Assert.Equal(AssessmentRecordJson.Serialize(initial.Assessment), AssessmentRecordJson.Serialize((await new PostgresAssessmentRecordStore(source).LoadAsync(initial.Assessment.AssessmentId))!));
        await Assert.ThrowsAsync<CaseReviewConflictException>(() => store.InitializeAsync(initial));
        Assert.Null(await store.LoadAsync(new("synthetic-missing-" + Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public async Task Concurrent_reviews_have_one_database_winner()
    {
        await using var source = Source();
        var initial = await Seed(source);
        async Task<bool> Attempt(string suffix)
        {
            try
            {
                await new CaseReviewService(Store(source), new Authorizer(true)).ReviewAsync(Actor,
                Command(initial) with { ReviewId = new("synthetic-review-" + suffix) }, Workflow, Policy); return true;
            }
            catch (CaseReviewConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attempt("one"), Attempt("two"));
        Assert.Single(results, success => success);
        var loaded = (await Store(source).LoadAsync(initial.Process.CaseId))!;
        Assert.Equal(1, loaded.Process.Revision);
        Assert.Equal(2, loaded.Audit.Events.Count);
    }

    [Fact]
    public async Task Denial_stale_and_invalid_append_leave_both_states_unchanged()
    {
        await using var source = Source();
        var initial = await Seed(source);
        var store = Store(source);
        await Assert.ThrowsAsync<CaseReviewDeniedException>(() => new CaseReviewService(store, new Authorizer(false)).ReviewAsync(Actor, Command(initial), Workflow, Policy));
        await Assert.ThrowsAsync<CaseReviewConflictException>(() => new CaseReviewService(store, new Authorizer(true)).ReviewAsync(Actor,
            Command(initial) with { ExpectedAuditRevision = 7 }, Workflow, Policy));
        await Assert.ThrowsAsync<CaseReviewIntegrityException>(() => store.ExecuteAsync(initial.Process.CaseId,
            state => state with { Process = state.Process.Apply(Workflow, 0, "accept") }));
        Assert.Equal(CaseReviewStateJson.Serialize(initial), CaseReviewStateJson.Serialize((await store.LoadAsync(initial.Process.CaseId))!));
    }

    [Fact]
    public async Task Database_rejects_history_update_delete_and_failed_insert_rolls_back()
    {
        await using var source = Source();
        var initial = await Seed(source);
        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[] { "UPDATE normacase.case_review_versions SET state_sha256=state_sha256 WHERE case_id=$1", "DELETE FROM normacase.case_review_versions WHERE case_id=$1" })
        {
            await using var mutation = new NpgsqlCommand(sql, connection);
            mutation.Parameters.AddWithValue(initial.Process.CaseId.Value);
            var error = await Assert.ThrowsAsync<PostgresException>(() => mutation.ExecuteNonQueryAsync());
            Assert.Equal("55000", error.SqlState);
        }
        var store = Store(source);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteAsync(initial.Process.CaseId,
            state => throw new InvalidOperationException("Synthetic callback failure")));
        Assert.Equal(CaseReviewStateJson.Serialize(initial), CaseReviewStateJson.Serialize((await store.LoadAsync(initial.Process.CaseId))!));
    }

    [Fact]
    public async Task Rejected_database_insert_rolls_back_process_and_audit()
    {
        await using var source = Source();
        var initial = await Seed(source);
        var name = "synthetic_review_failure_" + Guid.NewGuid().ToString("N");
        await using var connection = await source.OpenConnectionAsync();
        // Both identifiers are locally generated synthetic GUIDs, never user input.
        await using var add = new NpgsqlCommand($"ALTER TABLE normacase.case_review_versions ADD CONSTRAINT {name} CHECK (case_id <> '{initial.Process.CaseId.Value}' OR version = 0)", connection);
        await add.ExecuteNonQueryAsync();
        try
        {
            var error = await Assert.ThrowsAsync<CaseReviewStorageException>(() => new CaseReviewService(Store(source), new Authorizer(true))
                .ReviewAsync(Actor, Command(initial), Workflow, Policy));
            Assert.Equal("Case review storage operation failed.", error.Message);
            Assert.Equal(CaseReviewStateJson.Serialize(initial), CaseReviewStateJson.Serialize((await Store(source).LoadAsync(initial.Process.CaseId))!));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"ALTER TABLE normacase.case_review_versions DROP CONSTRAINT {name}", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Corrupted_persisted_checksum_is_rejected_before_deserialization()
    {
        await using var source = Source();
        var initial = await Seed(source);
        await using var connection = await source.OpenConnectionAsync();
        await using var disable = new NpgsqlCommand("ALTER TABLE normacase.case_review_versions DISABLE TRIGGER case_review_versions_no_update", connection);
        await disable.ExecuteNonQueryAsync();
        try
        {
            await using var corrupt = new NpgsqlCommand("UPDATE normacase.case_review_versions SET state_sha256=repeat('0',64) WHERE case_id=$1", connection);
            corrupt.Parameters.AddWithValue(initial.Process.CaseId.Value);
            await corrupt.ExecuteNonQueryAsync();
        }
        finally
        {
            await using var enable = new NpgsqlCommand("ALTER TABLE normacase.case_review_versions ENABLE TRIGGER case_review_versions_no_update", connection);
            await enable.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<CaseReviewIntegrityException>(() => Store(source).LoadAsync(initial.Process.CaseId));
    }

    [Fact]
    public async Task Changed_original_assessment_metadata_invalidates_the_aggregate()
    {
        await using var source = Source();
        var initial = await Seed(source);
        await using var connection = await source.OpenConnectionAsync();
        await using var disable = new NpgsqlCommand("ALTER TABLE normacase.assessment_records DISABLE TRIGGER assessment_records_no_update", connection);
        await disable.ExecuteNonQueryAsync();
        try
        {
            await using var corrupt = new NpgsqlCommand("UPDATE normacase.assessment_records SET record_format_version=999 WHERE assessment_id=$1", connection);
            corrupt.Parameters.AddWithValue(initial.Assessment.AssessmentId.Value);
            await corrupt.ExecuteNonQueryAsync();
        }
        finally
        {
            await using var enable = new NpgsqlCommand("ALTER TABLE normacase.assessment_records ENABLE TRIGGER assessment_records_no_update", connection);
            await enable.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<CaseReviewIntegrityException>(() => Store(source).LoadAsync(initial.Process.CaseId));
    }

    [Fact]
    public async Task Initialization_cannot_bind_a_changed_original_assessment()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var initial = Initial();
        await new PostgresAssessmentRecordStore(source).AppendAsync(initial.Assessment);
        var changed = initial.Assessment with { };
        var bad = new AssessmentRecord(changed.AssessmentId, changed.CaseId, changed.KnowledgePackId,
            "synthetic-other-platform", changed.RecordedAtUtc, changed.Input, changed.Result);
        await Assert.ThrowsAsync<CaseReviewIntegrityException>(() => Store(source).InitializeAsync(initial with { Assessment = bad }));
        Assert.Null(await Store(source).LoadAsync(initial.Process.CaseId));
    }

    [Fact]
    public async Task Fresh_assessment_and_aggregate_initialize_atomically_and_concurrently()
    {
        await using var source = Source();
        await new PostgresMigrationRunner(source).MigrateAsync();
        var initial = Initial();
        var store = Store(source);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => store.InitializeRecordedAsync(initial)));
        Assert.All(results, result => Assert.Equal(CaseReviewStateJson.Serialize(initial), CaseReviewStateJson.Serialize(result)));
        Assert.NotNull(await new PostgresAssessmentRecordStore(source).LoadAsync(initial.Assessment.AssessmentId));
        var reviewed = await new CaseReviewService(store, new Authorizer(true)).ReviewAsync(Actor, Command(initial), Workflow, Policy);
        Assert.Equal(CaseReviewStateJson.Serialize(reviewed), CaseReviewStateJson.Serialize(await store.InitializeRecordedAsync(initial)));
        var failed = Initial();
        var name = "synthetic_init_failure_" + Guid.NewGuid().ToString("N");
        await using var connection = await source.OpenConnectionAsync();
        await using var add = new NpgsqlCommand($"ALTER TABLE normacase.case_review_versions ADD CONSTRAINT {name} CHECK (case_id <> '{failed.Process.CaseId.Value}')", connection);
        await add.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<CaseReviewStorageException>(() => store.InitializeRecordedAsync(failed));
            Assert.Null(await new PostgresAssessmentRecordStore(source).LoadAsync(failed.Assessment.AssessmentId));
            Assert.Null(await store.LoadAsync(failed.Process.CaseId));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"ALTER TABLE normacase.case_review_versions DROP CONSTRAINT {name}", connection);
            await drop.ExecuteNonQueryAsync();
        }
        Assert.NotNull(await store.InitializeRecordedAsync(failed));
    }

    private static PostgresCaseReviewStore Store(NpgsqlDataSource source) => new(source, (id, version) =>
        id == Workflow.Id && version == Workflow.Version ? Workflow : throw new ArgumentException("Unknown synthetic workflow"));
    private sealed class Authorizer(bool allowed) : ICaseReviewAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState state, CaseReviewCommand command, CaseReviewPolicy policy) => allowed;
    }
    private static CaseReviewCommand Command(CaseReviewState state, HumanReviewDisposition disposition = HumanReviewDisposition.AcceptSystemResult)
        => new(state.Process.CaseId, state.Assessment.AssessmentId, new("synthetic-review"), 1, 0, 1, Time.AddMinutes(1),
            disposition, "Synthetic database review", disposition == HumanReviewDisposition.Override ? AssessmentOutcome.NotSupported : null);
    private static async Task<CaseReviewState> Seed(NpgsqlDataSource source)
    {
        await new PostgresMigrationRunner(source).MigrateAsync();
        var state = Initial();
        await new PostgresAssessmentRecordStore(source).AppendAsync(state.Assessment);
        await Store(source).InitializeAsync(state);
        return state;
    }
    private static CaseReviewState Initial()
    {
        var id = Guid.NewGuid().ToString("N");
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-c-pack.json"));
        var record = new AssessmentRecorder().Evaluate(pack,
            new Dictionary<string, CaseValue> { ["request_confirmed"] = TruthValue.Yes, ["measurement"] = 15.1234567890123456789012345m, ["alternative_confirmed"] = TruthValue.No },
            new DateOnly(2026, 10, 3), null, new(new("assessment-" + id), new("case-" + id), "synthetic-platform", Time));
        return new(record, 1, CaseProcessingInstance.Start(record.CaseId, 1, Workflow),
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, record.AssessmentId, Time, "synthetic-ingest")));
    }
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("NORMACASE_POSTGRES_TEST_CONNECTION is required."));
}
