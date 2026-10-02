using NormaCase.Domain.Audit;

namespace NormaCase.Application.Assessments;

public sealed record AssessmentExecutionContext
{
    public AssessmentExecutionContext(
        AssessmentId assessmentId,
        string caseId,
        string platformVersion,
        DateTimeOffset recordedAtUtc)
    {
        if (assessmentId.IsEmpty)\n            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        if (recordedAtUtc == default
            || recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Recording timestamp must be an explicit UTC value.",
                nameof(recordedAtUtc));
        }

        AssessmentId = assessmentId;
        CaseId = caseId;
        PlatformVersion = platformVersion;
        RecordedAtUtc = recordedAtUtc;
    }

    public AssessmentId AssessmentId { get; }
    public string CaseId { get; }
    public string PlatformVersion { get; }
    public DateTimeOffset RecordedAtUtc { get; }
}
