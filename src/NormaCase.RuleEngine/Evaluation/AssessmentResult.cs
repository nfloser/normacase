using NormaCase.Domain.Decision;

namespace NormaCase.RuleEngine.Evaluation;

public sealed record AssessmentResult(
    string KnowledgeRelease,
    DateOnly AssessmentDate,
    AssessmentOutcome Outcome,
    IReadOnlyList<string> MissingRequiredFields,
    RuleTrace? RuleTrace)
{
    public IReadOnlyList<CalculationTrace> Calculations { get; init; } = [];
}
