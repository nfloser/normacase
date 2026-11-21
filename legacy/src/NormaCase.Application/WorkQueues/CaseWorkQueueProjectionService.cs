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
    public CaseWorkQueueMembership Project(
        CaseProcessingInstance process,
        CaseWorkQueueConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(configuration);

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
            matches.SingleOrDefault()?.QueueId);
    }

    public CaseWorkItemProjection Project(
        AssessmentRecord assessment,
        AssessmentRouting routing,
        CaseProcessingInstance process,
        CaseWorkQueueConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(routing);

        var membership = Project(process, configuration);

        if (assessment.CaseId != routing.CaseId
            || assessment.CaseId != membership.CaseId
            || assessment.AssessmentId != routing.AssessmentId)
        {
            throw new CaseWorkQueueIdentityMismatchException();
        }

        return new(
            membership,
            assessment.AssessmentId,
            assessment.Result.Outcome,
            routing);
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
