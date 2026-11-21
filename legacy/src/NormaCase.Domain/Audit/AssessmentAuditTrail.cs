using System.Collections.ObjectModel;

namespace NormaCase.Domain.Audit;

public sealed class AssessmentAuditTrail
{
    private readonly ReadOnlyCollection<AssessmentAuditEvent> _events;

    private AssessmentAuditTrail(IEnumerable<AssessmentAuditEvent> events)
        => _events = Array.AsReadOnly(events.ToArray());

    public IReadOnlyList<AssessmentAuditEvent> Events => _events;

    public AssessmentId AssessmentId => _events[0].AssessmentId;

    public static AssessmentAuditTrail Start(AssessmentAuditEvent created)
    {
        ArgumentNullException.ThrowIfNull(created);

        if (created.Kind != AuditEventKind.AssessmentCreated)
            throw new ArgumentException("Audit trail must start with an assessment-created event.", nameof(created));
        if (created.Sequence != 1)
            throw new ArgumentException("First audit event must have sequence 1.", nameof(created));

        return new([created]);
    }

    public AssessmentAuditTrail Append(AssessmentAuditEvent next)
    {
        ArgumentNullException.ThrowIfNull(next);

        var previous = _events[^1];
        if (next.AssessmentId != AssessmentId)
            throw new ArgumentException("Audit event belongs to a different assessment.", nameof(next));
        if (next.Sequence != previous.Sequence + 1)
            throw new ArgumentException("Audit sequence must be contiguous and append-only.", nameof(next));
        if (next.Kind == AuditEventKind.AssessmentCreated)
            throw new ArgumentException("Assessment-created can only be the first audit event.", nameof(next));
        if (next.OccurredAt < previous.OccurredAt)
            throw new ArgumentException("Audit event timestamp cannot move backwards.", nameof(next));

        return new(_events.Append(next));
    }
}
