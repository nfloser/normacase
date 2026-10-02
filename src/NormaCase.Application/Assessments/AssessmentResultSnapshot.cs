using NormaCase.RuleEngine.Evaluation;

namespace NormaCase.Application.Assessments;

internal static class AssessmentResultSnapshot
{
    internal static AssessmentResult Copy(AssessmentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new(
            result.KnowledgeRelease,
            result.AssessmentDate,
            result.Outcome,
            Array.AsReadOnly(
                result.MissingRequiredFields.ToArray()),
            result.RuleTrace is null
                ? null
                : Copy(result.RuleTrace))
        {
            DomainOutputs = Array.AsReadOnly(
                result.DomainOutputs
                    .Select(Copy)
                    .ToArray())
        };
    }

    private static RuleTrace Copy(RuleTrace trace)
        => trace with
        {
            Condition = Copy(trace.Condition),
            Source = trace.Source with { }
        };

    private static DomainOutputTrace Copy(
        DomainOutputTrace trace)
        => trace with
        {
            Condition = Copy(trace.Condition),
            Source = trace.Source with { }
        };

    private static ConditionTrace Copy(
        ConditionTrace trace)
        => trace with
        {
            Children = Array.AsReadOnly(
                trace.Children
                    .Select(Copy)
                    .ToArray()),
            NumericExpression = trace.NumericExpression is null
                ? null
                : Copy(trace.NumericExpression)
        };

    private static NumericExpressionTrace Copy(
        NumericExpressionTrace trace)
        => trace with
        {
            Children = Array.AsReadOnly(
                trace.Children
                    .Select(Copy)
                    .ToArray())
        };
}
