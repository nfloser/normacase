namespace NormaCase.Application.Assessments;

public sealed record AssessmentExecutionContext
{
    public AssessmentExecutionContext(
        string assessmentId,
        string caseId,
        string platformVersion,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assessmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        if (recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Recording timestamp must be UTC.", nameof(recordedAtUtc));

        AssessmentId = assessmentId;
        CaseId = caseId;
        PlatformVersion = platformVersion;
        RecordedAtUtc = recordedAtUtc;
    }

    public string AssessmentId { get; }
    public string CaseId { get; }
    public string PlatformVersion { get; }
    public DateTimeOffset RecordedAtUtc { get; }
}
