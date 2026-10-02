using NormaCase.Domain.Audit;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Application.Assessments;

public sealed record AssessmentRecord
{
    public AssessmentRecord(
        AssessmentId assessmentId,
        string caseId,
        string knowledgePackId,
        string platformVersion,
        DateTimeOffset recordedAtUtc,
        AssessmentInputSnapshot input,
        AssessmentResult result)
    {
        if (assessmentId.IsEmpty)\n            throw new ArgumentException("Assessment id must be explicit.", nameof(assessmentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgePackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platformVersion);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);

        if (recordedAtUtc == default
            || recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Recording timestamp must be an explicit UTC value.",
                nameof(recordedAtUtc));
        }
        if (input.AssessmentDate != result.AssessmentDate)
            throw new ArgumentException("Input and result assessment dates must match.", nameof(result));
        if (string.IsNullOrWhiteSpace(result.KnowledgeRelease))
            throw new ArgumentException("Knowledge Release is required.", nameof(result));

        AssessmentId = assessmentId;
        CaseId = caseId;
        KnowledgePackId = knowledgePackId;
        PlatformVersion = platformVersion;
        RecordedAtUtc = recordedAtUtc;
        Input = input;
        Result = AssessmentResultSnapshot.Copy(result);
    }

    public AssessmentId AssessmentId { get; }
    public string CaseId { get; }
    public string KnowledgePackId { get; }
    public string PlatformVersion { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public AssessmentInputSnapshot Input { get; }
    public AssessmentResult Result { get; }
}
