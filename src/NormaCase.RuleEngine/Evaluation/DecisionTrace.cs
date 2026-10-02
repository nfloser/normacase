using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;

namespace NormaCase.RuleEngine.Evaluation;

public sealed record ConditionTrace(
    string Kind,
    ConditionResult Result,
    string? Field,
    CaseValue? Expected,
    CaseValue? Actual,
    decimal? Minimum,
    decimal? Maximum,
    IReadOnlyList<ConditionTrace> Children);

public sealed record RuleTrace(
    string RuleId,
    int RuleVersion,
    string SourceId,
    ConditionResult ConditionResult,
    AssessmentOutcome Outcome,
    ConditionTrace Condition);
