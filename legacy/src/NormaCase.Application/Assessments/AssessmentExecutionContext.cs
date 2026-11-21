using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;

namespace NormaCase.Application.Assessments;

public sealed record AssessmentExecutionContext
{
    public AssessmentExecutionContext(
        AssessmentId assessmentId,
        CaseId caseId,
        string platformVersion,
        DateTimeOffset recordedAtUtc)
    {
        if (assessmentId.IsEmpty)
            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        if (caseId.IsEmpty)
            throw new ArgumentException("Case id must be explicit.", nameof(caseId));
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
    public CaseId CaseId { get; }
    public string PlatformVersion { get; }
    public DateTimeOffset RecordedAtUtc { get; }
}
