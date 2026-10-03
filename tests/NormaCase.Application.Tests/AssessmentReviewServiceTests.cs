using NormaCase.Application.Assessments;
using NormaCase.Application.Audit;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Serialization;
using Xunit;

namespace NormaCase.Application.Tests;

public sealed class AssessmentReviewServiceTests
{
    private static readonly DateTimeOffset RecordedAt =
        new(2026, 10, 3, 1, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task Initialize_uses_the_persisted_assessment_identity_and_timestamp()
    {
        var record = Assessment("assessment-review-001");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);

        var trail = await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        var created = Assert.Single(trail.Events);
        Assert.Equal(AuditEventKind.AssessmentCreated, created.Kind);
        Assert.Equal(record.AssessmentId, created.AssessmentId);
        Assert.Equal(record.RecordedAtUtc, created.OccurredAt);
        Assert.Equal("system:assessment-recorder", created.ActorId);
        Assert.Equal(1, audits.AppendCalls);
        Assert.Equal(trail, audits.Latest);
    }

    [Fact]
    public async Task Accepted_review_appends_exactly_one_event_without_mutating_the_assessment()
    {
        var record = Assessment("assessment-review-002");
        var originalOutcome = record.Result.Outcome;
        var originalRelease = record.Result.KnowledgeRelease;
        var originalTimestamp = record.RecordedAtUtc;
        var originalCriterion =
            record.Input.Facts["criterion_a"];

        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);
        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        var reviewedAt = RecordedAt.AddMinutes(10);
        var trail = await service.RecordReviewAsync(
            new AssessmentReviewRequest(
                new ReviewId("review-accept-001"),
                record.AssessmentId,
                "reviewer-synth-01",
                reviewedAt,
                HumanReviewDisposition.AcceptSystemResult,
                "Synthetische Bestätigung des Systemergebnisses."));

        Assert.Equal(2, trail.Events.Count);
        Assert.Equal(2, audits.AppendCalls);
        var review = trail.Events[^1].Review!;
        Assert.Equal("review-accept-001", review.ReviewId.Value);
        Assert.Equal(reviewedAt, review.RecordedAt);
        Assert.Equal(
            HumanReviewDisposition.AcceptSystemResult,
            review.Disposition);
        Assert.Null(review.OverrideOutcome);

        var persisted = await records.LoadAsync(record.AssessmentId);
        Assert.NotNull(persisted);
        Assert.Equal(originalOutcome, persisted.Result.Outcome);
        Assert.Equal(originalRelease, persisted.Result.KnowledgeRelease);
        Assert.Equal(originalTimestamp, persisted.RecordedAtUtc);
        Assert.Equal(
            originalCriterion,
            persisted.Input.Facts["criterion_a"]);
    }

    [Fact]
    public async Task Override_review_preserves_generic_override_metadata()
    {
        var record = Assessment("assessment-review-003");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);
        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        var trail = await service.RecordReviewAsync(
            new AssessmentReviewRequest(
                new ReviewId("review-override-001"),
                record.AssessmentId,
                "reviewer-synth-02",
                RecordedAt.AddMinutes(20),
                HumanReviewDisposition.Override,
                "Synthetische Abweichung erfordert manuelle Prüfung.",
                AssessmentOutcome.HumanReview,
                new ReviewReference(
                    "synthetic-ticket",
                    "SYNTH-REVIEW-42")));

        var review = trail.Events[^1].Review!;
        Assert.Equal(
            HumanReviewDisposition.Override,
            review.Disposition);
        Assert.Equal(
            AssessmentOutcome.HumanReview,
            review.OverrideOutcome);
        Assert.Equal(
            "SYNTH-REVIEW-42",
            review.Reference!.Value);
    }

    [Fact]
    public async Task Initialization_requires_an_existing_assessment()
    {
        var service = new AssessmentReviewService(
            new MemoryRecordStore(),
            new MemoryAuditStore());

        await Assert.ThrowsAsync<
            AssessmentReviewAssessmentNotFoundException>(
            () => service.InitializeAsync(
                new AssessmentId("assessment-missing"),
                "system:assessment-recorder"));
    }

    [Fact]
    public async Task Initialization_rejects_a_store_record_for_a_different_assessment()
    {
        var returned = Assessment("assessment-returned-other");
        var requested =
            new AssessmentId("assessment-requested");
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(
            new AlwaysRecordStore(returned),
            audits);

        var exception = await Assert.ThrowsAsync<
            AssessmentReviewAssessmentBindingException>(
            () => service.InitializeAsync(
                requested,
                "system:assessment-recorder"));

        Assert.Equal(requested, exception.AssessmentId);
        Assert.Equal(0, audits.LoadCalls);
        Assert.Equal(0, audits.AppendCalls);
    }

    [Fact]
    public async Task Duplicate_initialization_is_rejected_before_append()
    {
        var record = Assessment("assessment-review-004");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);

        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        await Assert.ThrowsAsync<
            AssessmentReviewAlreadyInitializedException>(
            () => service.InitializeAsync(
                record.AssessmentId,
                "system:assessment-recorder"));

        Assert.Equal(1, audits.AppendCalls);
    }

    [Fact]
    public async Task Review_requires_an_existing_assessment_and_audit_trail()
    {
        var missingAssessment = new AssessmentReviewService(
            new MemoryRecordStore(),
            new MemoryAuditStore());

        await Assert.ThrowsAsync<
            AssessmentReviewAssessmentNotFoundException>(
            () => missingAssessment.RecordReviewAsync(
                Request("assessment-missing")));

        var record = Assessment("assessment-review-005");
        var missingAudit = new AssessmentReviewService(
            new MemoryRecordStore(record),
            new MemoryAuditStore());

        await Assert.ThrowsAsync<
            AssessmentReviewAuditNotInitializedException>(
            () => missingAudit.RecordReviewAsync(
                Request(record.AssessmentId.Value)));
    }

    [Fact]
    public async Task Review_timestamp_cannot_move_before_existing_history()
    {
        var record = Assessment("assessment-review-006");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);
        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordReviewAsync(
                new AssessmentReviewRequest(
                    new ReviewId("review-too-early"),
                    record.AssessmentId,
                    "reviewer-synth-01",
                    record.RecordedAtUtc.AddTicks(-1),
                    HumanReviewDisposition.AcceptSystemResult,
                    "Synthetischer Zeitreihenfehler.")));

        Assert.Equal(1, audits.AppendCalls);
    }

    [Fact]
    public async Task Existing_domain_review_invariants_are_not_bypassed()
    {
        var record = Assessment("assessment-review-007");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);
        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordReviewAsync(
                new AssessmentReviewRequest(
                    new ReviewId("review-invalid-accept"),
                    record.AssessmentId,
                    "reviewer-synth-01",
                    RecordedAt.AddMinutes(5),
                    HumanReviewDisposition.AcceptSystemResult,
                    "Synthetische Bestätigung.",
                    AssessmentOutcome.Supported)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordReviewAsync(
                new AssessmentReviewRequest(
                    new ReviewId("review-invalid-override"),
                    record.AssessmentId,
                    "reviewer-synth-01",
                    RecordedAt.AddMinutes(6),
                    HumanReviewDisposition.Override,
                    "Synthetische Abweichung.")));

        Assert.Equal(1, audits.AppendCalls);
    }

    [Fact]
    public async Task Review_metadata_is_validated_before_store_access()
    {
        var record = Assessment("assessment-review-invalid-metadata");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordReviewAsync(
                new AssessmentReviewRequest(
                    new ReviewId("review-invalid-metadata"),
                    record.AssessmentId,
                    " ",
                    RecordedAt.AddMinutes(5),
                    HumanReviewDisposition.AcceptSystemResult,
                    "Synthetische Bestätigung.")));

        Assert.Equal(0, records.LoadCalls);
        Assert.Equal(0, audits.LoadCalls);
        Assert.Equal(0, audits.AppendCalls);
    }

    [Fact]
    public async Task Review_rejects_audit_history_not_bound_to_the_persisted_record_timestamp()
    {
        var record = Assessment("assessment-review-binding");
        var mismatched = AssessmentAuditTrail.Start(
            AssessmentAuditEvent.AssessmentCreated(
                1,
                record.AssessmentId,
                record.RecordedAtUtc.AddMinutes(1),
                "system:assessment-recorder"));
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore(mismatched);
        var service = new AssessmentReviewService(records, audits);

        await Assert.ThrowsAsync<AssessmentReviewAuditBindingException>(
            () => service.RecordReviewAsync(
                Request(record.AssessmentId.Value)));

        Assert.Equal(0, audits.AppendCalls);
        Assert.Single(audits.Latest!.Events);
    }

    [Fact]
    public async Task Storage_conflicts_are_propagated_without_retrying_or_changing_the_event()
    {
        var record = Assessment("assessment-review-008");
        var records = new MemoryRecordStore(record);
        var audits = new MemoryAuditStore();
        var service = new AssessmentReviewService(records, audits);
        await service.InitializeAsync(
            record.AssessmentId,
            "system:assessment-recorder");

        audits.FailNextAppend =
            new AssessmentAuditTrailConflictException(
                record.AssessmentId);

        var request = Request(record.AssessmentId.Value);

        var exception = await Assert.ThrowsAsync<
            AssessmentAuditTrailConflictException>(
            () => service.RecordReviewAsync(request));

        Assert.Equal(record.AssessmentId, exception.AssessmentId);
        Assert.Equal(2, audits.AppendCalls);
        Assert.Equal(2, audits.LastAttempted!.Events.Count);
        Assert.Equal(
            request.ReviewId,
            audits.LastAttempted.Events[^1].Review!.ReviewId);
        Assert.Equal(
            request.RecordedAtUtc,
            audits.LastAttempted.Events[^1].OccurredAt);
        Assert.Single(audits.Latest!.Events);
    }

    private static AssessmentReviewRequest Request(
        string assessmentId)
        => new(
            new ReviewId("review-synth-default"),
            new AssessmentId(assessmentId),
            "reviewer-synth-01",
            RecordedAt.AddMinutes(30),
            HumanReviewDisposition.AcceptSystemResult,
            "Synthetische Bestätigung.");

    private static AssessmentRecord Assessment(string id)
        => new AssessmentRecorder().Evaluate(
            new KnowledgePackLoader().LoadFromFile(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "Fixtures",
                    "demo-a-pack.json")),
            new Dictionary<string, CaseValue>
            {
                ["criterion_a"] = TruthValue.Yes,
                ["criterion_b"] = TruthValue.Yes
            },
            new DateOnly(2026, 10, 3),
            evidence: null,
            new AssessmentExecutionContext(
                new AssessmentId(id),
                new CaseId("case-" + id),
                "platform-synth-1",
                RecordedAt));

    private sealed class AlwaysRecordStore
        : IAssessmentRecordStore
    {
        private readonly AssessmentRecord _record;

        public AlwaysRecordStore(AssessmentRecord record)
            => _record = record;

        public Task AppendAsync(
            AssessmentRecord record,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<AssessmentRecord?> LoadAsync(
            AssessmentId assessmentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AssessmentRecord?>(_record);
    }

    private sealed class MemoryRecordStore
        : IAssessmentRecordStore
    {
        private readonly Dictionary<AssessmentId, AssessmentRecord>
            _records = [];

        public int LoadCalls { get; private set; }

        public MemoryRecordStore(
            params AssessmentRecord[] records)
        {
            foreach (var record in records)
                _records.Add(record.AssessmentId, record);
        }

        public Task AppendAsync(
            AssessmentRecord record,
            CancellationToken cancellationToken = default)
        {
            _records.Add(record.AssessmentId, record);
            return Task.CompletedTask;
        }

        public Task<AssessmentRecord?> LoadAsync(
            AssessmentId assessmentId,
            CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            _records.TryGetValue(
                assessmentId,
                out var record);
            return Task.FromResult(record);
        }
    }

    private sealed class MemoryAuditStore
        : IAssessmentAuditTrailStore
    {
        public MemoryAuditStore(
            AssessmentAuditTrail? latest = null)
        {
            Latest = latest;
        }

        public AssessmentAuditTrail? Latest { get; private set; }

        public AssessmentAuditTrail? LastAttempted { get; private set; }

        public int AppendCalls { get; private set; }

        public int LoadCalls { get; private set; }

        public Exception? FailNextAppend { get; set; }

        public Task AppendAsync(
            AssessmentAuditTrail trail,
            CancellationToken cancellationToken = default)
        {
            AppendCalls++;
            LastAttempted = trail;

            if (FailNextAppend is { } failure)
            {
                FailNextAppend = null;
                return Task.FromException(failure);
            }

            Latest = trail;
            return Task.CompletedTask;
        }

        public Task<AssessmentAuditTrail?> LoadLatestAsync(
            AssessmentId assessmentId,
            CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return Task.FromResult(
                Latest is not null
                    && Latest.AssessmentId == assessmentId
                    ? Latest
                    : null);
        }
    }
}
