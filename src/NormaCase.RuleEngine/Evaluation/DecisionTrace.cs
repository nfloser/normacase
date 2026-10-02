using NormaCase.Domain.Decision;

namespace NormaCase.RuleEngine.Evaluation;

public sealed record ConditionTrace(
    string Kind,
    ConditionResult Result,
    string? Field,
    TruthValue? Expected,
    TruthValue? Actual,
    IReadOnlyList<ConditionTrace> Children);

public sealed record RuleTrace(
    string RuleId,
    int RuleVersion,
    string SourceId,
    ConditionResult ConditionResult,
    AssessmentOutcome Outcome,
    ConditionTrace Condition);
