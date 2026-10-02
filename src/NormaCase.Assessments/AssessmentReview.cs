using NormaCase.Domain.Decision;

namespace NormaCase.Assessments;

public enum AssessmentReviewAction
{
    ConfirmSystemOutcome,
    OverrideSystemOutcome,
    ReturnForCompletion
}

public sealed class AssessmentReview
{
    private const int MaximumReasonLength = 2000;

    private AssessmentReview(
        string reviewId,
        string assessmentId,
        string actorId,
        DateTimeOffset reviewedAtUtc,
        AssessmentReviewAction action,
        AssessmentOutcome originalOutcome,
        AssessmentOutcome? overrideOutcome,
        string reason)
    {
        ReviewId = reviewId;
        AssessmentId = assessmentId;
        ActorId = actorId;
        ReviewedAtUtc = reviewedAtUtc;
        Action = action;
        OriginalOutcome = originalOutcome;
        OverrideOutcome = overrideOutcome;
        Reason = reason;
    }

    public string ReviewId { get; }

    public string AssessmentId { get; }

    public string ActorId { get; }

    public DateTimeOffset ReviewedAtUtc { get; }

    public AssessmentReviewAction Action { get; }

    public AssessmentOutcome OriginalOutcome { get; }

    public AssessmentOutcome? OverrideOutcome { get; }

    public string Reason { get; }

    public static AssessmentReview CreateConfirmation(
        string reviewId,
        RecordedAssessment assessment,
        string actorId,
        DateTimeOffset reviewedAtUtc,
        string reason)
        => Create(
            reviewId,
            assessment,
            actorId,
            reviewedAtUtc,
            AssessmentReviewAction.ConfirmSystemOutcome,
            null,
            reason);

    public static AssessmentReview CreateOverride(
        string reviewId,
        RecordedAssessment assessment,
        string actorId,
        DateTimeOffset reviewedAtUtc,
        AssessmentOutcome overrideOutcome,
        string reason)
        => Create(
            reviewId,
            assessment,
            actorId,
            reviewedAtUtc,
            AssessmentReviewAction.OverrideSystemOutcome,
            overrideOutcome,
            reason);

    public static AssessmentReview CreateReturnForCompletion(
        string reviewId,
        RecordedAssessment assessment,
        string actorId,
        DateTimeOffset reviewedAtUtc,
        string reason)
        => Create(
            reviewId,
            assessment,
            actorId,
            reviewedAtUtc,
            AssessmentReviewAction.ReturnForCompletion,
            null,
            reason);

    private static AssessmentReview Create(
        string reviewId,
        RecordedAssessment assessment,
        string actorId,
        DateTimeOffset reviewedAtUtc,
        AssessmentReviewAction action,
        AssessmentOutcome? overrideOutcome,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        var validReviewId = TechnicalIdentifier.Require(
            reviewId,
            nameof(reviewId));
        var validActorId = TechnicalIdentifier.Require(
            actorId,
            nameof(actorId));
        var validReviewedAt = TechnicalIdentifier.RequireUtc(
            reviewedAtUtc,
            nameof(reviewedAtUtc));

        if (validReviewedAt < assessment.RecordedAtUtc)
        {
            throw new ArgumentException(
                "Review timestamp cannot precede the assessment record.",
                nameof(reviewedAtUtc));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > MaximumReasonLength)
        {
            throw new ArgumentException(
                "Review reason exceeds the supported length.",
                nameof(reason));
        }

        if (action == AssessmentReviewAction.OverrideSystemOutcome)
        {
            if (overrideOutcome is null)
            {
                throw new ArgumentException(
                    "An override outcome is required.",
                    nameof(overrideOutcome));
            }

            if (overrideOutcome == assessment.SystemOutcome)
            {
                throw new ArgumentException(
                    "An override must differ from the system outcome.",
                    nameof(overrideOutcome));
            }
        }
        else if (overrideOutcome is not null)
        {
            throw new ArgumentException(
                "Only an override action may carry an override outcome.",
                nameof(overrideOutcome));
        }

        return new(
            validReviewId,
            assessment.AssessmentId,
            validActorId,
            validReviewedAt,
            action,
            assessment.SystemOutcome,
            overrideOutcome,
            reason);
    }
}
