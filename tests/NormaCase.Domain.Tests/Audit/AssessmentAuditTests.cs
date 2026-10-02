using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;
using Xunit;

namespace NormaCase.Domain.Tests.Audit;

public sealed class AssessmentAuditTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Assessment_identity_is_explicit_and_rejects_blank_values()
    {
        var id = new AssessmentId("assessment-synth-001");

        Assert.Equal("assessment-synth-001", id.Value);
        Assert.Throws<ArgumentException>(() => new AssessmentId("   "));
    }

    [Fact]
    public void Accepted_review_keeps_the_system_outcome_separate()
    {
        var review = new HumanReviewRecord(
            new ReviewId("review-synth-001"),
            new AssessmentId("assessment-synth-001"),
            "reviewer-synth-01",
            CreatedAt.AddMinutes(10),
            HumanReviewDisposition.AcceptSystemResult,
            "Synthetic review confirms the deterministic system result.");

        Assert.Equal(HumanReviewDisposition.AcceptSystemResult, review.Disposition);
        Assert.Null(review.OverrideOutcome);
    }

    [Fact]
    public void Override_review_requires_an_explicit_generic_outcome_and_reason()
    {
        var review = new HumanReviewRecord(
            new ReviewId("review-synth-002"),
            new AssessmentId("assessment-synth-001"),
            "reviewer-synth-01",
            CreatedAt.AddMinutes(10),
            HumanReviewDisposition.Override,
            "Synthetic review requires manual handling.",
            AssessmentOutcome.HumanReview,
            new ReviewReference("synthetic-ticket", "SYNTH-42"));

        Assert.Equal(AssessmentOutcome.HumanReview, review.OverrideOutcome);
        Assert.Equal("SYNTH-42", review.Reference!.Value);
    }

    [Fact]
    public void Review_rejects_missing_reason_actor_or_utc_timestamp()
    {
        var assessmentId = new AssessmentId("assessment-synth-001");
        var reviewId = new ReviewId("review-synth-003");

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId, assessmentId, " ", CreatedAt, HumanReviewDisposition.AcceptSystemResult, "Reason"));

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId, assessmentId, "reviewer", CreatedAt, HumanReviewDisposition.AcceptSystemResult, " "));

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId,
            assessmentId,
            "reviewer",
            new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(2)),
            HumanReviewDisposition.AcceptSystemResult,
            "Reason"));

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId,
            assessmentId,
            "reviewer",
            default,
            HumanReviewDisposition.AcceptSystemResult,
            "Reason"));
    }

    [Fact]
    public void Review_rejects_inconsistent_or_unknown_override_values()
    {
        var assessmentId = new AssessmentId("assessment-synth-001");
        var reviewId = new ReviewId("review-synth-004");

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId,
            assessmentId,
            "reviewer",
            CreatedAt,
            HumanReviewDisposition.AcceptSystemResult,
            "Reason",
            AssessmentOutcome.Supported));

        Assert.Throws<ArgumentException>(() => new HumanReviewRecord(
            reviewId,
            assessmentId,
            "reviewer",
            CreatedAt,
            HumanReviewDisposition.Override,
            "Reason"));

        Assert.Throws<ArgumentOutOfRangeException>(() => new HumanReviewRecord(
            reviewId,
            assessmentId,
            "reviewer",
            CreatedAt,
            HumanReviewDisposition.Override,
            "Reason",
            (AssessmentOutcome)999));
    }

    [Fact]
    public void Audit_trail_is_append_only_and_sequence_is_explicit()
    {
        var assessmentId = new AssessmentId("assessment-synth-001");
        var created = AssessmentAuditEvent.AssessmentCreated(
            1,
            assessmentId,
            CreatedAt,
            "system:rule-engine");
        var trail = AssessmentAuditTrail.Start(created);

        var review = new HumanReviewRecord(
            new ReviewId("review-synth-005"),
            assessmentId,
            "reviewer-synth-01",
            CreatedAt.AddMinutes(15),
            HumanReviewDisposition.Override,
            "Synthetic reviewer sends the result to human review.",
            AssessmentOutcome.HumanReview);
        var recorded = AssessmentAuditEvent.HumanReviewRecorded(2, review);

        var appended = trail.Append(recorded);

        Assert.Single(trail.Events);
        Assert.Equal(2, appended.Events.Count);
        Assert.Equal(AuditEventKind.AssessmentCreated, appended.Events[0].Kind);
        Assert.Equal(AuditEventKind.HumanReviewRecorded, appended.Events[1].Kind);
        Assert.Same(review, appended.Events[1].Review);
    }

    [Fact]
    public void Audit_trail_rejects_duplicate_creation_or_backwards_time()
    {
        var assessmentId = new AssessmentId("assessment-synth-001");
        var trail = AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(
            1,
            assessmentId,
            CreatedAt,
            "system:rule-engine"));

        var duplicateCreation = AssessmentAuditEvent.AssessmentCreated(
            2,
            assessmentId,
            CreatedAt.AddMinutes(1),
            "system:rule-engine");
        Assert.Throws<ArgumentException>(() => trail.Append(duplicateCreation));

        var earlierReview = new HumanReviewRecord(
            new ReviewId("review-synth-006"),
            assessmentId,
            "reviewer-synth-01",
            CreatedAt.AddMinutes(-1),
            HumanReviewDisposition.AcceptSystemResult,
            "Synthetic review timestamp is deliberately earlier.");
        Assert.Throws<ArgumentException>(() =>
            trail.Append(AssessmentAuditEvent.HumanReviewRecorded(2, earlierReview)));

        Assert.Throws<ArgumentException>(() => AssessmentAuditEvent.AssessmentCreated(
            1,
            assessmentId,
            default,
            "system:rule-engine"));
    }

    [Fact]
    public void Audit_trail_rejects_reordering_or_cross_assessment_events()
    {
        var assessmentId = new AssessmentId("assessment-synth-001");
        var trail = AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(
            1,
            assessmentId,
            CreatedAt,
            "system:rule-engine"));

        var wrongSequence = AssessmentAuditEvent.AssessmentCreated(
            3,
            assessmentId,
            CreatedAt.AddMinutes(1),
            "system:rule-engine");
        Assert.Throws<ArgumentException>(() => trail.Append(wrongSequence));

        var otherAssessment = AssessmentAuditEvent.AssessmentCreated(
            2,
            new AssessmentId("assessment-synth-002"),
            CreatedAt.AddMinutes(1),
            "system:rule-engine");
        Assert.Throws<ArgumentException>(() => trail.Append(otherAssessment));
    }
}
