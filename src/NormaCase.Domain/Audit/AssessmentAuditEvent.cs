namespace NormaCase.Domain.Audit;

public enum AuditEventKind
{
    AssessmentCreated = 1,
    HumanReviewRecorded = 2
}

public sealed class AssessmentAuditEvent
{
    private AssessmentAuditEvent(
        long sequence,
        AuditEventKind kind,
        AssessmentId assessmentId,
        DateTimeOffset occurredAt,
        string actorId,
        HumanReviewRecord? review)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Audit sequence must be positive.");
        if (assessmentId.IsEmpty)
            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Audit timestamp must be an explicit UTC value.", nameof(occurredAt));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Audit actor id must not be blank.", nameof(actorId));

        Sequence = sequence;
        Kind = kind;
        AssessmentId = assessmentId;
        OccurredAt = occurredAt;
        ActorId = actorId;
        Review = review;
    }

    public long Sequence { get; }

    public AuditEventKind Kind { get; }

    public AssessmentId AssessmentId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string ActorId { get; }

    public HumanReviewRecord? Review { get; }

    public static AssessmentAuditEvent AssessmentCreated(
        long sequence,
        AssessmentId assessmentId,
        DateTimeOffset occurredAt,
        string actorId)
        => new(
            sequence,
            AuditEventKind.AssessmentCreated,
            assessmentId,
            occurredAt,
            actorId,
            null);

    public static AssessmentAuditEvent HumanReviewRecorded(
        long sequence,
        HumanReviewRecord review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new(
            sequence,
            AuditEventKind.HumanReviewRecorded,
            review.AssessmentId,
            review.RecordedAt,
            review.ActorId,
            review);
    }
}
