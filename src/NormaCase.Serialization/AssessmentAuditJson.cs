using System.Text.Json;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Decision;

namespace NormaCase.Serialization;

// Transport records deliberately carry every field, including explicit nulls.
public sealed record AuditReviewData(string ReviewId, AssessmentId AssessmentId,
    string ActorId, DateTimeOffset RecordedAt, HumanReviewDisposition Disposition,
    string Reason, AssessmentOutcome? OverrideOutcome, ReviewReference? Reference);
public sealed record AuditEventData(long Sequence, AuditEventKind Kind,
    AssessmentId AssessmentId, DateTimeOffset OccurredAt, string ActorId,
    AuditReviewData? Review);
public sealed record AssessmentAuditDocument(int FormatVersion, IReadOnlyList<AuditEventData> Events);

public static class AssessmentAuditJson
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(AssessmentAuditTrail trail)
    {
        ArgumentNullException.ThrowIfNull(trail);
        var events = trail.Events.Select(e => new AuditEventData(e.Sequence, e.Kind,
            e.AssessmentId, e.OccurredAt, e.ActorId, e.Review is { } r
                ? new AuditReviewData(r.ReviewId.Value, r.AssessmentId, r.ActorId,
                    r.RecordedAt, r.Disposition, r.Reason, r.OverrideOutcome, r.Reference)
                : null)).ToArray();
        return JsonSerializer.Serialize(new AssessmentAuditDocument(CurrentFormatVersion, events),
            InterchangeJson.Options);
    }

    public static AssessmentAuditTrail Deserialize(string json)
    {
        try
        {
            var document = InterchangeJson.Read<AssessmentAuditDocument>(json);
            if (document.FormatVersion != CurrentFormatVersion || document.Events.Count == 0)
                throw new JsonException("Unsupported or empty audit document.");

            AssessmentAuditTrail? trail = null;
            foreach (var data in document.Events)
            {
                if (data is null)
                    throw new JsonException("Audit event is required.");
                AssessmentAuditEvent next;
                switch (data.Kind)
                {
                    case AuditEventKind.AssessmentCreated when data.Review is null:
                        next = AssessmentAuditEvent.AssessmentCreated(data.Sequence,
                            data.AssessmentId, data.OccurredAt, data.ActorId);
                        break;
                    case AuditEventKind.HumanReviewRecorded when data.Review is { } r:
                        var review = new HumanReviewRecord(new ReviewId(r.ReviewId),
                            r.AssessmentId, r.ActorId, r.RecordedAt, r.Disposition,
                            r.Reason, r.OverrideOutcome, r.Reference);
                        if (data.AssessmentId != review.AssessmentId ||
                            data.OccurredAt != review.RecordedAt ||
                            data.OccurredAt.Offset != TimeSpan.Zero ||
                            data.ActorId != review.ActorId)
                            throw new JsonException("Inconsistent audit review metadata.");
                        next = AssessmentAuditEvent.HumanReviewRecorded(data.Sequence, review);
                        break;
                    default:
                        throw new JsonException("Invalid audit event.");
                }
                trail = trail is null ? AssessmentAuditTrail.Start(next) : trail.Append(next);
            }
            return trail!;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Constructor failures can contain imported values; do not expose them.
            throw new JsonException("Invalid audit document.");
        }
    }
}
