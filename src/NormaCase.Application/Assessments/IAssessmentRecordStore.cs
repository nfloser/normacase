using NormaCase.Domain.Audit;

namespace NormaCase.Application.Assessments;

public interface IAssessmentRecordStore
{
    Task AppendAsync(
        AssessmentRecord record,
        CancellationToken cancellationToken = default);

    Task<AssessmentRecord?> LoadAsync(
        AssessmentId assessmentId,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentRecordAlreadyExistsException : Exception
{
    public AssessmentRecordAlreadyExistsException(AssessmentId assessmentId)
        : base($"Assessment record '{assessmentId}' already exists.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentRecordIntegrityException : Exception
{
    public AssessmentRecordIntegrityException(AssessmentId assessmentId)
        : base($"Stored assessment record '{assessmentId}' failed integrity verification.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}
