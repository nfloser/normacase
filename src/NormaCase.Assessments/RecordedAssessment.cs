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
        var input = CaseInputJson.Deserialize(
            RequireDocument(caseInputJson, nameof(caseInputJson)));
        var assessment = AssessmentJson.Deserialize(
            RequireDocument(assessmentJson, nameof(assessmentJson)));

        ValidateDocumentAgreement(input, assessment);

        return new(
            TechnicalIdentifier.Require(
                assessmentId,
                nameof(assessmentId)),
            TechnicalIdentifier.Require(
                caseId,
                nameof(caseId)),
            TechnicalIdentifier.RequireUtc(
                recordedAtUtc,
                nameof(recordedAtUtc)),
            assessment.Assessment.AssessmentDate,
            assessment.PlatformVersion,
            assessment.Assessment.KnowledgeRelease,
            assessment.Assessment.Outcome,
            caseInputJson,
            assessmentJson,
            Fingerprint(caseInputJson),
            Fingerprint(assessmentJson));
    }

    public static RecordedAssessment Restore(
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
        var input = CaseInputJson.Deserialize(
            RequireDocument(caseInputJson, nameof(caseInputJson)));
        var assessment = AssessmentJson.Deserialize(
            RequireDocument(assessmentJson, nameof(assessmentJson)));

        ValidateDocumentAgreement(input, assessment);

        if (assessmentDate != assessment.Assessment.AssessmentDate
            || !string.Equals(
                platformVersion,
                assessment.PlatformVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                knowledgeRelease,
                assessment.Assessment.KnowledgeRelease,
                StringComparison.Ordinal)
            || systemOutcome != assessment.Assessment.Outcome)
        {
            throw new InvalidDataException(
                "Persisted assessment metadata does not match the assessment document.");
        }

        if (!string.Equals(
                caseInputSha256,
                Fingerprint(caseInputJson),
                StringComparison.Ordinal)
            || !string.Equals(
                assessmentSha256,
                Fingerprint(assessmentJson),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Persisted assessment fingerprint does not match the exact document.");
        }

        return new(
            TechnicalIdentifier.Require(
                assessmentId,
                nameof(assessmentId)),
            TechnicalIdentifier.Require(
                caseId,
                nameof(caseId)),
            TechnicalIdentifier.RequireUtc(
                recordedAtUtc,
                nameof(recordedAtUtc)),
            assessmentDate,
            platformVersion,
            knowledgeRelease,
            systemOutcome,
            caseInputJson,
            assessmentJson,
            caseInputSha256,
            assessmentSha256);
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

    private static string RequireDocument(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static void ValidateDocumentAgreement(
        CaseInput input,
        AssessmentDocument assessment)
    {
        if (input.AssessmentDate
            != assessment.Assessment.AssessmentDate)
        {
            throw new InvalidDataException(
                "Case input and assessment dates do not match.");
        }
    }

    private static string Fingerprint(string value)
        => Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value)));
}
