using System.Collections.ObjectModel;
using NormaCase.Application.Reviews;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Evidence;
using NormaCase.Knowledge.Model;

namespace NormaCase.Application.Corrections;

public sealed class CaseClarificationPolicy
{
    private readonly CaseCorrectionPolicy process;
    public CaseClarificationPolicy(string id, int version, string workflowId, int workflowVersion,
        IReadOnlyCollection<string> allowedStates, bool requireMissingTargets)
    {
        process = new(id, version, workflowId, workflowVersion, allowedStates);
        RequireMissingTargets = requireMissingTargets;
    }
    public string Id => process.Id;
    public int Version => process.Version;
    public string WorkflowId => process.WorkflowId;
    public int WorkflowVersion => process.WorkflowVersion;
    public bool RequireMissingTargets { get; }
    public bool Allows(string state) => process.Allows(state);
}

public sealed record CaseClarificationCommand(string ClarificationId, long ExpectedCaseRevision,
    long ExpectedProcessRevision, long ExpectedAuditRevision, DateTimeOffset RecordedAtUtc,
    string Reason, IReadOnlyList<string> RequestedFields, IReadOnlyList<string> RequestedEvidence);

public sealed class CaseClarificationRecord
{
    public CaseClarificationRecord(string clarificationId, CaseId caseId, string assessmentId,
        long caseRevision, long processRevision, long auditRevision, string policyId, int policyVersion,
        string actorId, string authenticationAuthority, DateTimeOffset recordedAtUtc, string reason,
        IReadOnlyList<string> requestedFields, IReadOnlyList<string> requestedEvidence)
    {
        CorrectionBoundary.Identifier(clarificationId); CorrectionBoundary.Identifier(policyId);
        CorrectionBoundary.Text(assessmentId, 256); CorrectionBoundary.Text(actorId, 256);
        CorrectionBoundary.Text(authenticationAuthority, 256); CorrectionBoundary.Text(reason, 1000);
        if (caseId.IsEmpty || caseRevision < 1 || processRevision < 0 || auditRevision < 1 || policyVersion < 1
            || recordedAtUtc == default || recordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid clarification binding.");
        RequestedFields = Snapshot(requestedFields); RequestedEvidence = Snapshot(requestedEvidence);
        if (RequestedFields.Count + RequestedEvidence.Count == 0) throw new ArgumentException("Explicit clarification targets required.");
        ClarificationId = clarificationId; CaseId = caseId; AssessmentId = assessmentId;
        CaseRevision = caseRevision; ProcessRevision = processRevision; AuditRevision = auditRevision;
        PolicyId = policyId; PolicyVersion = policyVersion; ActorId = actorId;
        AuthenticationAuthority = authenticationAuthority; RecordedAtUtc = recordedAtUtc; Reason = reason;
    }
    public string ClarificationId { get; }
    public CaseId CaseId { get; }
    public string AssessmentId { get; }
    public long CaseRevision { get; }
    public long ProcessRevision { get; }
    public long AuditRevision { get; }
    public string PolicyId { get; }
    public int PolicyVersion { get; }
    public string ActorId { get; }
    public string AuthenticationAuthority { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public string Reason { get; }
    public IReadOnlyList<string> RequestedFields { get; }
    public IReadOnlyList<string> RequestedEvidence { get; }
    private static IReadOnlyList<string> Snapshot(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > 128 || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new ArgumentException("Invalid clarification targets.");
        foreach (var value in values) CorrectionBoundary.Identifier(value);
        return new ReadOnlyCollection<string>(values.Order(StringComparer.Ordinal).ToArray());
    }
}

public interface ICaseClarificationAuthorizer
{
    bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current,
        CaseClarificationCommand command, CaseClarificationPolicy policy);
}

public sealed class CaseClarificationService(ICaseClarificationAuthorizer authorizer)
{
    private readonly ICaseClarificationAuthorizer authorization = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    public CaseClarificationRecord Prepare(AuthenticatedReviewActor actor, CaseReviewState current,
        CaseClarificationCommand command, CaseClarificationPolicy policy, KnowledgePack pack,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(policy); ArgumentNullException.ThrowIfNull(pack);
        if (!authorization.Authorize(actor, current, command, policy)) throw new CaseCorrectionDeniedException();
        if (current.Process.CaseRevision != command.ExpectedCaseRevision
            || current.Process.Revision != command.ExpectedProcessRevision
            || current.Audit.Events[^1].Sequence != command.ExpectedAuditRevision
            || command.RecordedAtUtc < current.Audit.Events[^1].OccurredAt)
            throw new CaseCorrectionConflictException();
        if (current.Process.WorkflowId != policy.WorkflowId || current.Process.WorkflowVersion != policy.WorkflowVersion
            || !policy.Allows(current.Process.StateId)) throw new CaseCorrectionPolicyException();
        if (pack.Manifest.PackId != current.Assessment.KnowledgePackId
            || pack.Manifest.ReleaseId != current.Assessment.Result.KnowledgeRelease)
            throw new CaseCorrectionBindingException();
        var record = new CaseClarificationRecord(command.ClarificationId, current.Process.CaseId,
            current.Assessment.AssessmentId.Value, command.ExpectedCaseRevision, command.ExpectedProcessRevision,
            command.ExpectedAuditRevision, policy.Id, policy.Version, actor.ActorId, actor.AuthenticationAuthority,
            command.RecordedAtUtc, command.Reason, command.RequestedFields, command.RequestedEvidence);
        var fields = pack.Fields.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var evidence = pack.EvidenceRequirements.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (record.RequestedFields.Any(x => !fields.Contains(x))
            || record.RequestedEvidence.Any(x => !evidence.Contains(x)))
            throw new ArgumentException("Clarification targets must belong to the retained schema.");
        if (policy.RequireMissingTargets && (record.RequestedFields.Any(x => current.Assessment.Input.Facts.TryGetValue(x, out var value) && !value.IsUnknown)
            || record.RequestedEvidence.Any(x => current.Assessment.Input.Evidence.GetValueOrDefault(x, EvidenceStatus.Missing) == EvidenceStatus.Present)))
            throw new CaseCorrectionPolicyException();
        token.ThrowIfCancellationRequested(); return record;
    }
    public static void VerifyResolution(CaseClarificationRecord request, CaseCorrectionPlan correction)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(correction);
        if (request.CaseId != correction.Link.CaseId || request.AssessmentId != correction.Link.PreviousAssessmentId.Value
            || request.CaseRevision != correction.Link.PreviousCaseRevision
            || request.ProcessRevision > correction.Link.PreviousProcessRevision || request.AuditRevision > correction.Link.PreviousAuditRevision
            || request.RecordedAtUtc > correction.Link.RecordedAtUtc)
            throw new CaseCorrectionBindingException();
        if (request.RequestedFields.Any(x => !correction.Input.Input.Facts.TryGetValue(x, out var value) || value.IsUnknown)
            || request.RequestedEvidence.Any(x => correction.Input.Input.Evidence.GetValueOrDefault(x, EvidenceStatus.Missing) != EvidenceStatus.Present))
            throw new CaseCorrectionPolicyException();
    }
}
