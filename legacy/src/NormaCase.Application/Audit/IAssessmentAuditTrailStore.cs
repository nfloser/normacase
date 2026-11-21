using NormaCase.Domain.Audit;

namespace NormaCase.Application.Audit;

public interface IAssessmentAuditTrailStore
{
    Task AppendAsync(
        AssessmentAuditTrail trail,
        CancellationToken cancellationToken = default);

    Task<AssessmentAuditTrail?> LoadLatestAsync(
        AssessmentId assessmentId,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentAuditTrailConflictException : Exception
{
    public AssessmentAuditTrailConflictException(AssessmentId assessmentId)
        : base($"Audit history for assessment '{assessmentId}' cannot be appended.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentAuditTrailAssessmentMissingException : Exception
{
    public AssessmentAuditTrailAssessmentMissingException(AssessmentId assessmentId)
        : base($"Assessment '{assessmentId}' does not exist.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentAuditTrailIntegrityException : Exception
{
    public AssessmentAuditTrailIntegrityException(AssessmentId assessmentId)
        : base($"Stored audit history for assessment '{assessmentId}' failed integrity verification.")
    {
        AssessmentId = assessmentId;
    }

    public AssessmentId AssessmentId { get; }
}

public sealed class AssessmentAuditTrailStorageException : Exception
{
    public AssessmentAuditTrailStorageException()
        : base("Assessment audit storage operation failed.")
    {
    }
}
