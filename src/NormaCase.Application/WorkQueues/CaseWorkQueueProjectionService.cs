using NormaCase.Application.Assessments;
using NormaCase.Application.Triage;
using NormaCase.Domain.Cases;

namespace NormaCase.Application.WorkQueues;

/// <summary>
/// Projects already-recorded case-process state into configurable work queues.
/// This service does not route, transition, approve, authorize or persist a case.
/// </summary>
public sealed class CaseWorkQueueProjectionService
{
    public CaseWorkItemProjection Project(
        AssessmentRecord assessment,
        AssessmentRouting routing,
        CaseProcessingInstance process,
        CaseWorkQueueConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(configuration);

        if (assessment.CaseId != routing.CaseId
            || assessment.CaseId != process.CaseId
            || assessment.AssessmentId != routing.AssessmentId)
        {
            throw new CaseWorkQueueIdentityMismatchException();
        }

        var matches = configuration.Queues
            .Where(queue =>
                string.Equals(queue.WorkflowId, process.WorkflowId, StringComparison.Ordinal)
                && queue.WorkflowVersion == process.WorkflowVersion
                && queue.ContainsState(process.StateId))
            .ToArray();

        if (matches.Length > 1)
            throw new CaseWorkQueueConfigurationException();

        return new(
            configuration,
            process,
            assessment.AssessmentId,
            assessment.Result.Outcome,
            routing,
            matches.SingleOrDefault()?.QueueId);
    }
}

public sealed class CaseWorkQueueIdentityMismatchException : InvalidOperationException
{
    public CaseWorkQueueIdentityMismatchException()
        : base("Assessment, routing and case-processing identities do not match.") { }
}

public sealed class CaseWorkQueueConfigurationException : InvalidOperationException
{
    public CaseWorkQueueConfigurationException()
        : base("Work queue configuration produced ambiguous membership.") { }
}
