using Npgsql;
using NormaCase.Application.Assessments;
using NormaCase.Application.Authorization;
using NormaCase.Application.Corrections;
using NormaCase.Application.Intake;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Serialization;
using NormaCase.Persistence.PostgreSql;
using NormaCase.Serialization;
using Xunit;

namespace NormaCase.Persistence.PostgreSql.Tests;

public sealed class PostgresCaseCorrectionStoreTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowDefinition Workflow = new("pg-correction-process", 1, "received",
        [new("received", false), new("waiting", false), new("pending", false), new("accepted", true)],
        [new("information", "received", "waiting"), new("ready", "received", "pending"), new("accept", "pending", "accepted")]);
    private static readonly ApprovalRoutingPolicy Triage = new("pg-correction-triage", 1,
        [AssessmentOutcome.Supported, AssessmentOutcome.NotSupported]);
    private static readonly CaseProcessingRoutingPolicy Routing = new("pg-correction-routing", 1,
        Triage.Id, 1, Workflow.Id, 1, new Dictionary<AssessmentRoutingDisposition, string> {
            [AssessmentRoutingDisposition.ReadyForApproval] = "ready", [AssessmentRoutingDisposition.Incomplete] = "information",
            [AssessmentRoutingDisposition.HumanReview] = "information" });
    private static readonly CaseCorrectionPolicy Policy = new("pg-information-completion", 1, Workflow.Id, 1, ["waiting", "pending"]);

    [Fact]
    public async Task Corrected_revision_retains_original_receipt_assessment_and_review_and_reloads_after_restart()
    {
        await using var source = Source();
        var fixture = await Seed(source);
        var originalJson = CaseReviewStateJson.Serialize(fixture.State);
        var result = await Correct(source, fixture);
        Assert.Equal(2, result.Next.Process.CaseRevision);
        Assert.Equal("pending", result.Next.Process.StateId);
        Assert.Single(result.Next.Audit.Events);
        Assert.Equal(CaseReviewStateJson.Serialize(result.Next), CaseReviewStateJson.Serialize((await Store(source).LoadAsync(fixture.State.Process.CaseId))!));
        var original = await new PostgresNormalizedIntakeStore(source).LoadAsync(fixture.Original.Provenance.SourceSystemId,
            fixture.Original.Provenance.UpstreamCaseId, 1);
        Assert.Equal(NormalizedIntakeJson.Serialize(fixture.Original), NormalizedIntakeJson.Serialize(original!));
        Assert.Equal(AssessmentRecordJson.Serialize(fixture.State.Assessment),
            AssessmentRecordJson.Serialize((await new PostgresAssessmentRecordStore(source).LoadAsync(fixture.State.Assessment.AssessmentId))!));
        var history = await Store(source).LoadHistoryPageAsync(fixture.State.Process.CaseId, 1, null);
        Assert.Equal(2, history.Count);
        Assert.Equal(originalJson, CaseReviewStateJson.Serialize(history[0].State));
        Assert.Null(history[0].Correction);
        Assert.Equal(result.Link, history[1].Correction);
        Assert.Equal(CaseReviewStateJson.Serialize(result.Next),
            CaseReviewStateJson.Serialize((await Store(source).LoadRevisionAsync(fixture.State.Process.CaseId, 1))!.State));
        var review = new CaseReviewCommand(result.Next.Process.CaseId, result.Next.Assessment.AssessmentId,
            new("pg-correction-review-" + Guid.NewGuid().ToString("N")), 2, 1, 1, Time.AddMinutes(2),
            HumanReviewDisposition.AcceptSystemResult, "Synthetische erneute Freigabe");
        var reviewPolicy = new CaseReviewPolicy("pg-correction-review-policy", 1, Workflow.Id, 1,
            new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.AcceptSystemResult] = "accept" });
        await new CaseReviewService(Store(source), new ReviewAuthorizer()).ReviewAsync(fixture.Actor, review, Workflow, reviewPolicy);
        Assert.Equal(2, (await Store(source).LoadAsync(fixture.State.Process.CaseId))!.Audit.Events.Count);
        Assert.Equal(originalJson, CaseReviewStateJson.Serialize((await Store(source).LoadRevisionAsync(fixture.State.Process.CaseId, 0))!.State));
        Assert.Single(await Store(source).LoadHistoryPageAsync(fixture.State.Process.CaseId, 1, 1));
    }

    [Fact]
    public async Task Concurrent_corrections_have_one_winner_and_one_new_revision()
    {
        await using var source = Source(); var fixture = await Seed(source);
        async Task<bool> Attempt()
        {
            try { await Correct(source, fixture); return true; }
            catch (CaseCorrectionConflictException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, success => success);
        Assert.Equal(2, (await Store(source).LoadAsync(fixture.State.Process.CaseId))!.Process.CaseRevision);
        Assert.Equal(2, (await Store(source).LoadHistoryPageAsync(fixture.State.Process.CaseId, 25, null)).Count);
    }

    [Fact]
    public async Task One_connection_authority_rolls_back_input_assessment_link_and_snapshot_on_callback_failure()
    {
        var builder = new NpgsqlConnectionStringBuilder(Connection()) { MaxPoolSize = 1, Timeout = 3 };
        await using var source = NpgsqlDataSource.Create(builder.ConnectionString); var fixture = await Seed(source);
        var command = Command(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PostgresReviewedEntitlementChangeStore(source)
            .ExecuteWithEffectiveLockAsync<int>(fixture.Actor.ActorId, async (_, token) => {
                await Service().CorrectAsync(Store(source), fixture.Actor, command, fixture.Pack,
                    Workflow, Policy, Triage, Routing, token);
                throw new InvalidOperationException("Synthetic failure after staged correction");
            }));
        Assert.Equal(CaseReviewStateJson.Serialize(fixture.State), CaseReviewStateJson.Serialize((await Store(source).LoadAsync(fixture.State.Process.CaseId))!));
        Assert.Null(await new PostgresAssessmentRecordStore(source).LoadAsync(command.AssessmentId));
        Assert.Equal(1, (await new PostgresNormalizedIntakeStore(source).LoadForCaseAsync(fixture.State.Process.CaseId))!.Provenance.UpstreamRevision);
        Assert.Single(await Store(source).LoadHistoryPageAsync(fixture.State.Process.CaseId, 25, null));
    }

    [Fact]
    public async Task Correction_requires_live_exact_case_and_action_authority()
    {
        await using var source = Source(); var fixture = await Seed(source);
        await Assert.ThrowsAsync<CaseCorrectionDeniedException>(() => Service().CorrectAsync(Store(source),
            fixture.Actor, Command(fixture), fixture.Pack, Workflow, Policy, Triage, Routing));
        var other = "synthetic-local:reader-" + Guid.NewGuid().ToString("N");
        var entitlements = new PostgresReviewedEntitlementChangeStore(source);
        await entitlements.ReconcileBaselineAsync(new(other, 0, ["READ"], [fixture.State.Process.CaseId.Value]));
        await Assert.ThrowsAsync<CaseCorrectionDeniedException>(() => entitlements.ExecuteWithEffectiveLockAsync(other,
            (_, token) => Service().CorrectAsync(Store(source), new(other, "synthetic-local"), Command(fixture),
                fixture.Pack, Workflow, Policy, Triage, Routing, token)));
        Assert.Single(await Store(source).LoadHistoryPageAsync(fixture.State.Process.CaseId, 25, null));
    }

    [Fact]
    public async Task Database_rejects_correction_history_mutation_and_stale_review_cannot_approve_new_assessment()
    {
        await using var source = Source(); var fixture = await Seed(source);
        var result = await Correct(source, fixture);
        await using var connection = await source.OpenConnectionAsync();
        foreach (var sql in new[] {
            "UPDATE normacase.case_correction_links SET link_sha256=link_sha256 WHERE case_id=$1",
            "DELETE FROM normacase.case_correction_links WHERE case_id=$1" })
        {
            await using var command = new NpgsqlCommand(sql, connection); command.Parameters.AddWithValue(fixture.State.Process.CaseId.Value);
            Assert.Equal("55000", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
        var reviewPolicy = new CaseReviewPolicy("pg-review-policy", 1, Workflow.Id, 1,
            new Dictionary<HumanReviewDisposition, string> { [HumanReviewDisposition.AcceptSystemResult] = "accept" });
        var stale = new CaseReviewCommand(fixture.State.Process.CaseId, result.Next.Assessment.AssessmentId,
            new("stale-review"), 1, 1, 1, Time.AddMinutes(2), HumanReviewDisposition.AcceptSystemResult, "Synthetische alte Revision");
        await Assert.ThrowsAsync<CaseReviewConflictException>(() => new CaseReviewService(Store(source), new ReviewAuthorizer())
            .ReviewAsync(fixture.Actor, stale, Workflow, reviewPolicy));
        Assert.Single((await Store(source).LoadAsync(fixture.State.Process.CaseId))!.Audit.Events);
    }

    private static Task<CaseCorrectionPlan> Correct(NpgsqlDataSource source, Fixture fixture)
        => new PostgresReviewedEntitlementChangeStore(source).ExecuteWithEffectiveLockAsync(fixture.Actor.ActorId,
            (_, token) => Service().CorrectAsync(Store(source), fixture.Actor, Command(fixture), fixture.Pack, Workflow, Policy, Triage, Routing, token));
    private static CaseCorrectionService Service() => new(new CorrectionAuthorizer());
    private sealed class CorrectionAuthorizer : ICaseCorrectionAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current, CaseCorrectionCommand command, CaseCorrectionPolicy policy) => true;
    }
    private sealed class ReviewAuthorizer : ICaseReviewAuthorizer
    {
        public bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current, CaseReviewCommand command, CaseReviewPolicy policy) => true;
    }
    private static CaseCorrectionCommand Command(Fixture fixture)
        => new("correction-" + Guid.NewGuid().ToString("N"), new("assessment-" + Guid.NewGuid().ToString("N")), 1, 1, 1,
            Time.AddMinutes(1), "current-platform", "Synthetisch nachgereichte Angaben",
            new(fixture.Original.CaseId, fixture.Original.CaseTypeId,
                new(fixture.Original.Provenance.SourceSystemId, fixture.Original.Provenance.UpstreamCaseId,
                    "message-" + Guid.NewGuid().ToString("N"), 2, "synthetic-json", 1, Time.AddMinutes(1)),
                fixture.Original.Input.AssessmentDate,
                new Dictionary<string, CaseValue> { ["criterion_a"] = TruthValue.Yes, ["criterion_b"] = TruthValue.Yes, ["criterion_c"] = TruthValue.No },
                new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(), new Dictionary<string, IReadOnlyList<string>>()));
    private static async Task<Fixture> Seed(NpgsqlDataSource source)
    {
        await new PostgresMigrationRunner(source).MigrateAsync();
        var id = Guid.NewGuid().ToString("N");
        var pack = new KnowledgePackLoader().LoadFromFile(Path.Combine(AppContext.BaseDirectory, "Fixtures/demo-a-pack.json"));
        var request = new NormalizedIntakeRequest(new("correction-case-" + id), "synthetic-case",
            new("source-" + id, "order-" + id, "message-one", 1, "synthetic-json", 1, Time), new DateOnly(2026, 10, 5),
            new Dictionary<string, CaseValue> { ["criterion_b"] = TruthValue.Yes, ["criterion_c"] = TruthValue.No },
            new Dictionary<string, NormaCase.Domain.Evidence.EvidenceStatus>(), new Dictionary<string, IReadOnlyList<string>>());
        var original = (await new NormalizedIntakeService(new PostgresNormalizedIntakeStore(source)).AcceptAsync(request, pack)).Record;
        var assessment = new AssessmentRecorder().Evaluate(pack, original.Input.Facts, original.Input.AssessmentDate,
            original.Input.Evidence, new(new("assessment-original-" + id), original.CaseId, "original-platform", Time));
        var state = new CaseReviewState(assessment, 1, CaseProcessingInstance.Start(original.CaseId, 1, Workflow).Apply(Workflow, 0, "information"),
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, assessment.AssessmentId, Time, "synthetic-intake")));
        await Store(source).InitializeRecordedAsync(state);
        var actor = new AuthenticatedReviewActor("synthetic-local:corrector-" + id, "synthetic-local");
        await new PostgresReviewedEntitlementChangeStore(source).ReconcileBaselineAsync(new(actor.ActorId, 0, ["READ", "CORRECT"], [original.CaseId.Value]));
        return new(pack, original, state, actor);
    }
    private sealed record Fixture(KnowledgePack Pack, NormalizedIntakeRecord Original, CaseReviewState State, AuthenticatedReviewActor Actor);
    private static PostgresCaseReviewStore Store(NpgsqlDataSource source)
        => new(source, (id, version) => id == Workflow.Id && version == 1 ? Workflow : throw new ArgumentException("Unknown synthetic workflow"));
    private static string Connection() => Environment.GetEnvironmentVariable("NORMACASE_POSTGRES_TEST_CONNECTION")
        ?? throw new InvalidOperationException("NORMACASE_POSTGRES_TEST_CONNECTION required.");
    private static NpgsqlDataSource Source() => NpgsqlDataSource.Create(Connection());
}
