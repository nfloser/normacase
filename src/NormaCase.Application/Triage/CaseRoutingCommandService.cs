using NormaCase.Application.Assessments;
using NormaCase.Application.Intake;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Workflow;

namespace NormaCase.Application.Triage;

public sealed record RoutingTransitionBinding(string StateId, AssessmentRoutingDisposition Disposition, string TransitionId);
public sealed record TechnicalTransitionBinding(string StateId, string TransitionId);

public sealed class CaseRoutingPolicy
{
    private readonly Dictionary<(string,AssessmentRoutingDisposition),string> transitions = [];
    private readonly Dictionary<string,string> technical = new(StringComparer.Ordinal);
    public CaseRoutingPolicy(string id,int version,WorkflowDefinition definition,ApprovalRoutingPolicy assessments,
        IEnumerable<RoutingTransitionBinding> bindings,IEnumerable<TechnicalTransitionBinding> technicalBindings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if(version<1) throw new ArgumentOutOfRangeException(nameof(version));
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(assessments);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(technicalBindings);
        Id=id; Version=version; Definition=definition; Assessments=assessments;
        foreach(var binding in bindings)
        {
            if(binding is null || !Enum.IsDefined(binding.Disposition)) throw new ArgumentException("Invalid routing binding.");
            ValidateTransition(binding.StateId,binding.TransitionId);
            if(transitions.Count>=256 || !transitions.TryAdd((binding.StateId,binding.Disposition),binding.TransitionId))
                throw new ArgumentException("Ambiguous or excessive routing bindings.");
        }
        foreach(var binding in technicalBindings)
        {
            if(binding is null) throw new ArgumentException("Invalid technical routing binding.");
            ValidateTransition(binding.StateId,binding.TransitionId);
            if(technical.Count>=256 || !technical.TryAdd(binding.StateId,binding.TransitionId))
                throw new ArgumentException("Ambiguous or excessive technical routing bindings.");
        }
        foreach(var ready in transitions.Where(item=>item.Key.Item2==AssessmentRoutingDisposition.ReadyForApproval))
        {
            definition.TryGetTransition(ready.Value,out var readyTransition);
            var otherIds=transitions.Where(item=>item.Key.Item1==ready.Key.Item1 && item.Key.Item2!=AssessmentRoutingDisposition.ReadyForApproval).Select(item=>item.Value);
            if(technical.TryGetValue(ready.Key.Item1,out var technicalId)) otherIds=otherIds.Append(technicalId);
            foreach(var otherId in otherIds)
            {
                definition.TryGetTransition(otherId,out var otherTransition);
                if(otherTransition!.ToStateId==readyTransition!.ToStateId)
                    throw new ArgumentException("Uncertain/technical routing cannot share an approval-preparation target.");
            }
        }
    }
    public string Id { get; }
    public int Version { get; }
    public WorkflowDefinition Definition { get; }
    public ApprovalRoutingPolicy Assessments { get; }
    internal string? Find(string state,AssessmentRoutingDisposition disposition)=>transitions.GetValueOrDefault((state,disposition));
    internal string? FindTechnical(string state)=>technical.GetValueOrDefault(state);
    private void ValidateTransition(string state,string transition)
    {
        if(string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(transition)
            || !Definition.TryGetTransition(transition,out var declared) || declared.FromStateId!=state)
            throw new ArgumentException("Routing transition is not declared from the configured state.");
    }
}

public enum RoutingPlanStatus { Planned, Unresolved }
public sealed class CaseRoutingCommandPlan
{
    internal CaseRoutingCommandPlan(NormalizedIntakeRecord intake,CaseProcessingInstance process,
        CaseRoutingPolicy policy,AssessmentRouting? assessment,string? transitionId)
    {
        CaseId=intake.CaseId; CaseRevision=process.CaseRevision; AssessmentId=assessment?.AssessmentId;
        ExpectedRevision=process.Revision; PolicyId=policy.Id; PolicyVersion=policy.Version;
        WorkflowId=process.WorkflowId; WorkflowVersion=process.WorkflowVersion;
        AssessmentRouting=assessment; TransitionId=transitionId;
    }
    public CaseId CaseId { get; }
    public long CaseRevision { get; }
    public AssessmentId? AssessmentId { get; }
    public long ExpectedRevision { get; }
    public string PolicyId { get; }
    public int PolicyVersion { get; }
    public string WorkflowId { get; }
    public int WorkflowVersion { get; }
    public AssessmentRouting? AssessmentRouting { get; }
    public string? TransitionId { get; }
    public RoutingPlanStatus Status=>TransitionId is null?RoutingPlanStatus.Unresolved:RoutingPlanStatus.Planned;
}

/// <summary>Proposes explicit transitions from exact recorded case/assessment facts; never applies or approves.</summary>
public sealed class CaseRoutingCommandService
{
    public CaseRoutingCommandPlan Plan(NormalizedIntakeRecord intake,AssessmentRecord assessment,
        CaseProcessingInstance process,CaseRoutingPolicy policy)
    {
        ValidateProcess(intake,process,policy);
        ArgumentNullException.ThrowIfNull(assessment);
        if(assessment.CaseId!=intake.CaseId || assessment.KnowledgePackId!=intake.KnowledgePackId
            || assessment.Result.KnowledgeRelease!=intake.KnowledgeRelease
            || assessment.Input.AssessmentDate!=intake.Input.AssessmentDate
            || assessment.RecordedAtUtc<intake.Provenance.ReceivedAtUtc
            || !Equal(assessment.Input.Facts,intake.Input.Facts) || !Equal(assessment.Input.Evidence,intake.Input.Evidence))
            throw new ArgumentException("Assessment is not bound to the normalized case revision.");
        var routing=new AssessmentTriageService().Route(assessment,policy.Assessments);
        return new(intake,process,policy,routing,policy.Find(process.StateId,routing.Disposition));
    }
    public CaseRoutingCommandPlan PlanTechnicalFailure(NormalizedIntakeRecord intake,
        CaseProcessingInstance process,CaseRoutingPolicy policy)
    {
        ValidateProcess(intake,process,policy);
        return new(intake,process,policy,null,policy.FindTechnical(process.StateId));
    }
    private static void ValidateProcess(NormalizedIntakeRecord intake,CaseProcessingInstance process,CaseRoutingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(policy);
        if(process.CaseId!=intake.CaseId || process.CaseRevision!=intake.Provenance.UpstreamRevision)
            throw new ArgumentException("Process is not bound to the normalized case revision.");
        _=process.IsTerminal(policy.Definition); // Validates exact workflow identity/version.
    }
    private static bool Equal<T>(IReadOnlyDictionary<string,T> left,IReadOnlyDictionary<string,T> right)
        =>left.Count==right.Count && left.All(item=>right.TryGetValue(item.Key,out var value)
            && EqualityComparer<T>.Default.Equals(item.Value,value));
}
