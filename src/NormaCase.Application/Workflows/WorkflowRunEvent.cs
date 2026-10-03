namespace NormaCase.Application.Workflows;

public sealed record WorkflowRunEvent
{
    public WorkflowRunEvent(
        string? transitionId,
        string actorId,
        DateTimeOffset recordedAtUtc,
        string reason,
        WorkflowExecutionSnapshot snapshot)
    {
        if (transitionId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(transitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (recordedAtUtc == default || recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Workflow event time must be explicit UTC.", nameof(recordedAtUtc));

        var service = new WorkflowExecutionService();
        Snapshot = service.Capture(service.Restore(snapshot));
        TransitionId = transitionId;
        ActorId = actorId;
        RecordedAtUtc = recordedAtUtc;
        Reason = reason;
    }

    // null identifies creation; every later event names an explicit transition.
    public string? TransitionId { get; }
    public string ActorId { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public string Reason { get; }
    public WorkflowExecutionSnapshot Snapshot { get; }
}
