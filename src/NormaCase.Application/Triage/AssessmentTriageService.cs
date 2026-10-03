using NormaCase.Application.Assessments;
using NormaCase.Domain.Audit;
using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Application.Triage;

public enum CaseProcessingState { ReadyForApproval, Incomplete, HumanReview }
public enum TriageReasonCode
{
    MissingRequiredField, IncompleteAssessment, AssessmentRequiresReview,
    MissingRuleTrace, UnknownCondition, UnknownNumericValue, UnknownOutput,
    UnresolvedEvidence, PolicyRequiresReview
}
public sealed record TriageReason(TriageReasonCode Code, string Reference);

public sealed class AssessmentRouting
{
    internal AssessmentRouting(AssessmentRecord record, ApprovalRoutingPolicy policy,
        CaseProcessingState state, IEnumerable<TriageReason> reasons)
    {
        CaseId = record.CaseId;
        AssessmentId = record.AssessmentId;
        PolicyId = policy.Id;
        PolicyVersion = policy.Version;
        State = state;
        Reasons = Array.AsReadOnly(reasons.Distinct().ToArray());
    }
    public CaseId CaseId { get; }
    public AssessmentId AssessmentId { get; }
    public string PolicyId { get; }
    public int PolicyVersion { get; }
    public CaseProcessingState State { get; }
    public IReadOnlyList<TriageReason> Reasons { get; }
}

/// <summary>Routes recorded engine results conservatively; does not evaluate or approve.</summary>
public sealed class AssessmentTriageService
{
    public AssessmentRouting Route(AssessmentRecord record, ApprovalRoutingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(policy);
        var reasons = new List<TriageReason>();
        var result = record.Result;
        foreach (var field in result.MissingRequiredFields.Order(StringComparer.Ordinal))
            reasons.Add(new(TriageReasonCode.MissingRequiredField, field));
        if (result.Outcome == AssessmentOutcome.Incomplete)
            reasons.Add(new(TriageReasonCode.IncompleteAssessment, record.AssessmentId.Value));
        if (result.Outcome == AssessmentOutcome.HumanReview)
            reasons.Add(new(TriageReasonCode.AssessmentRequiresReview, record.AssessmentId.Value));
        foreach (var item in record.Input.Evidence.OrderBy(item => item.Key, StringComparer.Ordinal))
            if (item.Value == EvidenceStatus.Conflicting)
                reasons.Add(new(TriageReasonCode.UnresolvedEvidence, item.Key));

        if (result.RuleTrace is null)
            reasons.Add(new(TriageReasonCode.MissingRuleTrace, record.AssessmentId.Value));
        else
            Inspect(result.RuleTrace.Condition, "rule", reasons);
        foreach (var output in result.DomainOutputs.OrderBy(item => item.OutputId, StringComparer.Ordinal))
        {
            if (output.Value.IsUnknown)
                reasons.Add(new(TriageReasonCode.UnknownOutput, output.OutputId));
            Inspect(output.Condition, "output/" + output.OutputId, reasons);
        }

        var incomplete = result.MissingRequiredFields.Count > 0 || result.Outcome == AssessmentOutcome.Incomplete;
        if (!incomplete && !policy.Allows(result.Outcome))
            reasons.Add(new(TriageReasonCode.PolicyRequiresReview, policy.Id));
        var state = incomplete ? CaseProcessingState.Incomplete
            : reasons.Count > 0 ? CaseProcessingState.HumanReview : CaseProcessingState.ReadyForApproval;
        return new(record, policy, state, reasons);
    }

    private static void Inspect(ConditionTrace trace, string path, List<TriageReason> reasons)
    {
        if (trace.Result == ConditionResult.Unknown)
            reasons.Add(new(TriageReasonCode.UnknownCondition, path));
        if (trace.EvidenceRequirementId is not null && trace.EvidenceStatus != EvidenceStatus.Present)
            reasons.Add(new(TriageReasonCode.UnresolvedEvidence, trace.EvidenceRequirementId));
        if (trace.NumericExpression is not null)
            InspectNumeric(trace.NumericExpression, path + "/numeric", reasons);
        for (var index = 0; index < trace.Children.Count; index++)
            Inspect(trace.Children[index], path + "/" + index, reasons);
    }

    private static void InspectNumeric(NumericExpressionTrace trace, string path, List<TriageReason> reasons)
    {
        if (trace.Value.IsUnknown)
            reasons.Add(new(TriageReasonCode.UnknownNumericValue, path));
        for (var index = 0; index < trace.Children.Count; index++)
            InspectNumeric(trace.Children[index], path + "/" + index, reasons);
    }
}
