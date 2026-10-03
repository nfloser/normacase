using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;

namespace NormaCase.Application.Audit;

public sealed record AssessmentReviewRequest
{
    public AssessmentReviewRequest(
        ReviewId reviewId,
        AssessmentId assessmentId,
        string actorId,
        DateTimeOffset recordedAtUtc,
        HumanReviewDisposition disposition,
        string reason,
        AssessmentOutcome? overrideOutcome = null,
        ReviewReference? reference = null)
    {
        ReviewId = reviewId;
        AssessmentId = assessmentId;
        ActorId = actorId;
        RecordedAtUtc = recordedAtUtc;
        Disposition = disposition;
        Reason = reason;
        OverrideOutcome = overrideOutcome;
        Reference = reference;
    }

    public ReviewId ReviewId { get; }

    public AssessmentId AssessmentId { get; }

    public string ActorId { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public HumanReviewDisposition Disposition { get; }

    public string Reason { get; }

    public AssessmentOutcome? OverrideOutcome { get; }

    public ReviewReference? Reference { get; }
}

public sealed class AssessmentReviewService
{
    private readonly IAssessmentRecordStore _assessmentRecords;
    private readonly IAssessmentAuditTrailStore _auditTrails;

    public AssessmentReviewService(
        IAssessmentRecordStore assessmentRecords,
        IAssessmentAuditTrailStore auditTrails)
    {
        ArgumentNullException.ThrowIfNull(assessmentRecords);
        ArgumentNullException.ThrowIfNull(auditTrails);

        _assessmentRecords = assessmentRecords;
        _auditTrails = auditTrails;
    }

    public async Task<AssessmentAuditTrail> InitializeAsync(
        AssessmentId assessmentId,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (assessmentId.IsEmpty)
        {
            throw new ArgumentException(
                "Assessment id must be explicit.",
                nameof(assessmentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var record = await _assessmentRecords.LoadAsync(
            assessmentId,
            cancellationToken);

        if (record is null)
        {
            throw new AssessmentReviewAssessmentNotFoundException(
                assessmentId);
        }

        var existing = await _auditTrails.LoadLatestAsync(
            assessmentId,
            cancellationToken);

        if (existing is not null)
        {
            throw new AssessmentReviewAlreadyInitializedException(
                assessmentId);
        }

        var created = AssessmentAuditEvent.AssessmentCreated(
            sequence: 1,
            assessmentId,
            record.RecordedAtUtc,
            actorId);

        var trail = AssessmentAuditTrail.Start(created);

        await _auditTrails.AppendAsync(
            trail,
            cancellationToken);

        return trail;
    }

    public async Task<AssessmentAuditTrail> RecordReviewAsync(
        AssessmentReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var review = new HumanReviewRecord(
            request.ReviewId,
            request.AssessmentId,
            request.ActorId,
            request.RecordedAtUtc,
            request.Disposition,
            request.Reason,
            request.OverrideOutcome,
            request.Reference);

        var assessment = await _assessmentRecords.LoadAsync(
            review.AssessmentId,
            cancellationToken);

        if (assessment is null)
        {
            throw new AssessmentReviewAssessmentNotFoundException(
                review.AssessmentId);
        }

        var current = await _auditTrails.LoadLatestAsync(
            review.AssessmentId,
            cancellationToken);

        if (current is null)
        {
            throw new AssessmentReviewAuditNotInitializedException(
                request.AssessmentId);
        }

        ValidateAuditBinding(assessment, current);

        var nextSequence = checked(
            current.Events[^1].Sequence + 1);

        var next = current.Append(
            AssessmentAuditEvent.HumanReviewRecorded(
                nextSequence,
                review));

        await _auditTrails.AppendAsync(
            next,
            cancellationToken);

        return next;
    }

    private static void ValidateAuditBinding(
        AssessmentRecord assessment,
        AssessmentAuditTrail trail)
    {
        var created = trail.Events[0];

        if (trail.AssessmentId != assessment.AssessmentId
            || created.Kind != AuditEventKind.AssessmentCreated
            || created.AssessmentId != assessment.AssessmentId
            || created.OccurredAt != assessment.RecordedAtUtc)
        {
            throw new AssessmentReviewAuditBindingException(
                assessment.AssessmentId);
        }
    }
}

public sealed class AssessmentReviewAssessmentNotFoundException
    : Exception
{
    public AssessmentReviewAssessmentNotFoundException(
        AssessmentId assessmentId)
        : base(
            $"Assessment '{assessmentId}' does not exist.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentReviewAlreadyInitializedException
    : Exception
{
    public AssessmentReviewAlreadyInitializedException(
        AssessmentId assessmentId)
        : base(
            $"Audit history for assessment '{assessmentId}' is already initialized.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentReviewAuditNotInitializedException
    : Exception
{
    public AssessmentReviewAuditNotInitializedException(
        AssessmentId assessmentId)
        : base(
            $"Audit history for assessment '{assessmentId}' has not been initialized.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentReviewAuditBindingException
    : Exception
{
    public AssessmentReviewAuditBindingException(
        AssessmentId assessmentId)
        : base(
            $"Audit history for assessment '{assessmentId}' is not bound to the stored assessment record.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}
