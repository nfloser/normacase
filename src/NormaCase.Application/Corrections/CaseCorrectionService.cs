using System.Collections.ObjectModel;
using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Application.Processing;
using NormaCase.Application.Reviews;
using NormaCase.Application.Triage;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;
using NormaCase.Knowledge.Model;

namespace NormaCase.Application.Corrections;

/// <summary>Explicit process permission; no medical rules and no implied actor authority.</summary>
public sealed class CaseCorrectionPolicy
{
    private readonly IReadOnlyCollection<string> states;
    public CaseCorrectionPolicy(string id, int version, string workflowId, int workflowVersion,
        IReadOnlyCollection<string> allowedStates)
    {
        CorrectionBoundary.Identifier(id); CorrectionBoundary.Identifier(workflowId);
        ArgumentNullException.ThrowIfNull(allowedStates);
        if (version < 1 || workflowVersion < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (allowedStates.Count is < 1 or > 64) throw new ArgumentException("Explicit bounded correction states required.");
        foreach (var state in allowedStates) CorrectionBoundary.Identifier(state);
        if (allowedStates.Distinct(StringComparer.Ordinal).Count() != allowedStates.Count)
            throw new ArgumentException("Duplicate correction state.");
        Id = id; Version = version; WorkflowId = workflowId; WorkflowVersion = workflowVersion;
        states = new ReadOnlyCollection<string>(allowedStates.Order(StringComparer.Ordinal).ToArray());
    }
    public string Id { get; }
    public int Version { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public bool Allows(string stateId) => states.Contains(stateId, StringComparer.Ordinal);
}

public sealed record CaseCorrectionCommand(string CorrectionId, AssessmentId AssessmentId,
    long ExpectedCaseRevision, long ExpectedProcessRevision, long ExpectedAuditRevision,
    DateTimeOffset RecordedAtUtc, string Reason, NormalizedIntakeRequest CorrectedInput);

/// <summary>Append-only relation to the exact preceding input and assessment, including its review revision.</summary>
public sealed record CaseCorrectionLink(string CorrectionId, CaseId CaseId,
    AssessmentId PreviousAssessmentId, AssessmentId AssessmentId,
    long PreviousCaseRevision, long PreviousProcessRevision, long PreviousAuditRevision, long CaseRevision,
    string SourceSystemId, string UpstreamCaseId, string PreviousMessageId, string MessageId,
    string PolicyId, int PolicyVersion, string ActorId, string AuthenticationAuthority,
    DateTimeOffset RecordedAtUtc, string Reason);

public sealed record CaseCorrectionPlan(NormalizedIntakeRecord Input, CaseReviewState Next, CaseCorrectionLink Link);

public interface ICaseCorrectionAuthorizer
{
    bool Authorize(AuthenticatedReviewActor actor, CaseReviewState current,
        CaseCorrectionCommand command, CaseCorrectionPolicy policy);
}

/// <summary>
/// Adapter must lock the current case and execute prepare once against its authoritative current
/// state and original receipt. Append the normalized input, assessment, relation and new review
/// snapshot in one live authorization transaction. Exceptions/cancellation roll back every append.
/// Serialize with human review; never retry against a newer revision or discard prior history.
/// </summary>
public interface ICaseCorrectionTransactionStore
{
    Task<CaseCorrectionPlan> ExecuteCorrectionAsync(CaseId caseId,
        Func<CaseReviewState, NormalizedIntakeRecord, CaseCorrectionPlan> prepare,
        CancellationToken cancellationToken = default);
}

public sealed class CaseCorrectionService(ICaseCorrectionAuthorizer authorizer)
{
    private readonly ICaseCorrectionAuthorizer authorization = authorizer ?? throw new ArgumentNullException(nameof(authorizer));

    public Task<CaseCorrectionPlan> CorrectAsync(ICaseCorrectionTransactionStore store,
        AuthenticatedReviewActor actor, CaseCorrectionCommand command, KnowledgePack pack,
        WorkflowDefinition workflow, CaseCorrectionPolicy policy, ApprovalRoutingPolicy triage,
        CaseProcessingRoutingPolicy routing, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.CorrectedInput);
        return store.ExecuteCorrectionAsync(command.CorrectedInput.CaseId,
            (current, original) => Prepare(actor, command, current, original, pack, workflow, policy, triage, routing, cancellationToken),
            cancellationToken);
    }

    // Deterministic preparation only: explicit versions/time/id, no network or storage side effects.
    public CaseCorrectionPlan Prepare(AuthenticatedReviewActor actor, CaseCorrectionCommand command,
        CaseReviewState current, NormalizedIntakeRecord original, KnowledgePack pack,
        WorkflowDefinition workflow, CaseCorrectionPolicy policy, ApprovalRoutingPolicy triage,
        CaseProcessingRoutingPolicy routing, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(pack); ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy); ArgumentNullException.ThrowIfNull(triage);
        ArgumentNullException.ThrowIfNull(routing); ArgumentNullException.ThrowIfNull(command.CorrectedInput);
        CorrectionBoundary.Identifier(command.CorrectionId);
        CorrectionBoundary.Text(command.Reason, 1000);
        if (command.AssessmentId.IsEmpty || command.ExpectedCaseRevision < 1
            || command.ExpectedProcessRevision < 0 || command.ExpectedAuditRevision < 1
            || command.RecordedAtUtc == default || command.RecordedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Explicit correction identity, revisions and UTC time required.");
        if (!authorization.Authorize(actor, current, command, policy)) throw new CaseCorrectionDeniedException();
        cancellationToken.ThrowIfCancellationRequested();
        if (current.Process.CaseRevision != command.ExpectedCaseRevision
            || current.Process.Revision != command.ExpectedProcessRevision
            || current.Audit.Events[^1].Sequence != command.ExpectedAuditRevision
            || command.AssessmentId == current.Assessment.AssessmentId
            || command.RecordedAtUtc < current.Audit.Events[^1].OccurredAt
            || original.Provenance.UpstreamRevision == long.MaxValue
            || command.CorrectedInput.Provenance.UpstreamRevision != original.Provenance.UpstreamRevision + 1
            || command.CorrectedInput.Provenance.MessageId == original.Provenance.MessageId
            || command.CorrectedInput.Provenance.ReceivedAtUtc < original.Provenance.ReceivedAtUtc
            || command.CorrectedInput.Provenance.ReceivedAtUtc > command.RecordedAtUtc)
            throw new CaseCorrectionConflictException();
        if (current.Process.CaseId != original.CaseId || current.Assessment.CaseId != original.CaseId
            || command.CorrectedInput.CaseId != original.CaseId
            || current.AssessmentCaseRevision != current.Process.CaseRevision
            || original.Provenance.UpstreamRevision != current.Process.CaseRevision
            || command.CorrectedInput.CaseTypeId != original.CaseTypeId
            || command.CorrectedInput.Provenance.SourceSystemId != original.Provenance.SourceSystemId
            || command.CorrectedInput.Provenance.UpstreamCaseId != original.Provenance.UpstreamCaseId
            || current.Audit.AssessmentId != current.Assessment.AssessmentId
            || current.Audit.Events[0].OccurredAt != current.Assessment.RecordedAtUtc
            || original.KnowledgePackId != current.Assessment.KnowledgePackId
            || original.KnowledgeRelease != current.Assessment.Result.KnowledgeRelease
            || pack.Manifest.PackId != original.KnowledgePackId || pack.Manifest.ReleaseId != original.KnowledgeRelease
            || !SameInput(original.Input, current.Assessment.Input))
            throw new CaseCorrectionBindingException();
        if (policy.WorkflowId != workflow.Id || policy.WorkflowVersion != workflow.Version
            || current.Process.WorkflowId != workflow.Id || current.Process.WorkflowVersion != workflow.Version
            || !policy.Allows(current.Process.StateId))
            throw new CaseCorrectionPolicyException();
        var input = new NormalizedIntakeService(new PreparationOnlyStore()).Normalize(command.CorrectedInput, pack);
        var assessment = new AssessmentRecorder().Evaluate(pack, input.Input.Facts, input.Input.AssessmentDate, input.Input.Evidence,
            new(command.AssessmentId, input.CaseId, current.Assessment.PlatformVersion, command.RecordedAtUtc));
        var revision = checked(current.Process.CaseRevision + 1);
        var start = CaseProcessingInstance.Start(input.CaseId, revision, workflow);
        var routed = new CaseProcessingRoutingService().Apply(new AssessmentTriageService().Route(assessment, triage),
            start, workflow, routing, revision, 0);
        if (routed.Status != CaseProcessingRoutingStatus.Applied) throw new CaseCorrectionPolicyException();
        var next = new CaseReviewState(assessment, revision, routed.Process,
            AssessmentAuditTrail.Start(AssessmentAuditEvent.AssessmentCreated(1, assessment.AssessmentId,
                assessment.RecordedAtUtc, actor.ActorId)));
        var link = new CaseCorrectionLink(command.CorrectionId, input.CaseId, current.Assessment.AssessmentId,
            assessment.AssessmentId, current.Process.CaseRevision, current.Process.Revision,
            current.Audit.Events[^1].Sequence, revision, original.Provenance.SourceSystemId, original.Provenance.UpstreamCaseId,
            original.Provenance.MessageId, input.Provenance.MessageId, policy.Id, policy.Version, actor.ActorId,
            actor.AuthenticationAuthority, command.RecordedAtUtc, command.Reason);
        cancellationToken.ThrowIfCancellationRequested();
        return new(input, next, link);
    }

    internal static bool SameInput(AssessmentInputSnapshot left, AssessmentInputSnapshot right)
        => left.AssessmentDate == right.AssessmentDate && Equal(left.Facts, right.Facts) && Equal(left.Evidence, right.Evidence);
    private static bool Equal<T>(IReadOnlyDictionary<string, T> left, IReadOnlyDictionary<string, T> right)
        => left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out var value)
            && EqualityComparer<T>.Default.Equals(item.Value, value));
    private sealed class PreparationOnlyStore : INormalizedIntakeStore
    {
        public Task<IntakeReceipt> AppendAsync(NormalizedIntakeRecord record, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Correction preparation cannot persist input.");
    }
}

public sealed class CaseCorrectionDeniedException : InvalidOperationException
{ public CaseCorrectionDeniedException() : base("Case correction is not authorized.") { } }
public sealed class CaseCorrectionConflictException : InvalidOperationException
{ public CaseCorrectionConflictException() : base("Case correction identity or revision conflicts.") { } }
public sealed class CaseCorrectionBindingException : InvalidOperationException
{ public CaseCorrectionBindingException() : base("Case correction input does not match the historical assessment.") { } }
public sealed class CaseCorrectionPolicyException : InvalidOperationException
{ public CaseCorrectionPolicyException() : base("Case correction policy cannot apply the configured process.") { } }

internal static class CorrectionBoundary
{
    internal static void Identifier(string value) => Text(value, 128);
    internal static void Text(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
            throw new ArgumentException("Explicit bounded correction text required.");
    }
}
