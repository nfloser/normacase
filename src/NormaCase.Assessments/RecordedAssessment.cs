using System.Security.Cryptography;
using System.Text;
using NormaCase.Domain.Decision;
using NormaCase.Serialization;

namespace NormaCase.Assessments;

public sealed class RecordedAssessment
{
    private RecordedAssessment(
        string assessmentId,
        string caseId,
        DateTimeOffset recordedAtUtc,
        DateOnly assessmentDate,
        string platformVersion,
        string knowledgeRelease,
        AssessmentOutcome systemOutcome,
        string caseInputJson,
        string assessmentJson,
        string caseInputSha256,
        string assessmentSha256)
    {
        AssessmentId = assessmentId;
        CaseId = caseId;
        RecordedAtUtc = recordedAtUtc;
        AssessmentDate = assessmentDate;
        PlatformVersion = platformVersion;
        KnowledgeRelease = knowledgeRelease;
        SystemOutcome = systemOutcome;
        CaseInputJson = caseInputJson;
        AssessmentJson = assessmentJson;
        CaseInputSha256 = caseInputSha256;
        AssessmentSha256 = assessmentSha256;
    }

    public string AssessmentId { get; }

    public string CaseId { get; }

    public DateTimeOffset RecordedAtUtc { get; }

    public DateOnly AssessmentDate { get; }

    public string PlatformVersion { get; }

    public string KnowledgeRelease { get; }

    public AssessmentOutcome SystemOutcome { get; }

    public string CaseInputJson { get; }

    public string AssessmentJson { get; }

    public string CaseInputSha256 { get; }

    public string AssessmentSha256 { get; }

    public static RecordedAssessment Create(
        string assessmentId,
        string caseId,
        DateTimeOffset recordedAtUtc,
        string caseInputJson,
        string assessmentJson)
    {
        var validAssessmentId = TechnicalIdentifier.Require(
            assessmentId,
            nameof(assessmentId));
        var validCaseId = TechnicalIdentifier.Require(
            caseId,
            nameof(caseId));
        var validRecordedAt = TechnicalIdentifier.RequireUtc(
            recordedAtUtc,
            nameof(recordedAtUtc));

        ArgumentException.ThrowIfNullOrWhiteSpace(caseInputJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(assessmentJson);

        var input = CaseInputJson.Deserialize(caseInputJson);
        var assessment = AssessmentJson.Deserialize(assessmentJson);

        if (input.AssessmentDate
            != assessment.Assessment.AssessmentDate)
        {
            throw new InvalidDataException(
                "Case input and assessment dates do not match.");
        }

        return new(
            validAssessmentId,
            validCaseId,
            validRecordedAt,
            assessment.Assessment.AssessmentDate,
            assessment.PlatformVersion,
            assessment.Assessment.KnowledgeRelease,
            assessment.Assessment.Outcome,
            caseInputJson,
            assessmentJson,
            Fingerprint(caseInputJson),
            Fingerprint(assessmentJson));
    }

    public bool HasValidFingerprints()
        => string.Equals(
                CaseInputSha256,
                Fingerprint(CaseInputJson),
                StringComparison.Ordinal)
            && string.Equals(
                AssessmentSha256,
                Fingerprint(AssessmentJson),
                StringComparison.Ordinal);

    private static string Fingerprint(string value)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
}
