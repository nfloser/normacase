using NormaCase.Domain.Decision;

namespace NormaCase.Domain.Audit;

public sealed class HumanReviewRecord
{
    public HumanReviewRecord(
        ReviewId reviewId,
        AssessmentId assessmentId,
        string actorId,
        DateTimeOffset recordedAt,
        HumanReviewDisposition disposition,
        string reason,
        AssessmentOutcome? overrideOutcome = null,
        ReviewReference? reference = null)
    {
        if (reviewId.IsEmpty)
            throw new ArgumentException("Review id must be explicit.", nameof(reviewId));
        if (assessmentId.IsEmpty)
            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Actor id must not be blank.", nameof(actorId));
        if (recordedAt == default || recordedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Review timestamp must be an explicit UTC value.", nameof(recordedAt));
        if (!Enum.IsDefined(disposition))
            throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Unknown review disposition.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Review reason must not be blank.", nameof(reason));

        if (overrideOutcome is not null && !Enum.IsDefined(overrideOutcome.Value))
            throw new ArgumentOutOfRangeException(nameof(overrideOutcome), overrideOutcome, "Unknown assessment outcome.");

        if (disposition == HumanReviewDisposition.AcceptSystemResult && overrideOutcome is not null)
            throw new ArgumentException("Accepted reviews cannot carry an override outcome.", nameof(overrideOutcome));

        if (disposition == HumanReviewDisposition.Override && overrideOutcome is null)
            throw new ArgumentException("Override reviews require an explicit generic outcome.", nameof(overrideOutcome));

        ReviewId = reviewId;
        AssessmentId = assessmentId;
        ActorId = actorId;
        RecordedAt = recordedAt;
        Disposition = disposition;
        Reason = reason;
        OverrideOutcome = overrideOutcome;
        Reference = reference;
    }

    public ReviewId ReviewId { get; }

    public AssessmentId AssessmentId { get; }

    public string ActorId { get; }

    public DateTimeOffset RecordedAt { get; }

    public HumanReviewDisposition Disposition { get; }

    public string Reason { get; }

    public AssessmentOutcome? OverrideOutcome { get; }

    public ReviewReference? Reference { get; }
}
