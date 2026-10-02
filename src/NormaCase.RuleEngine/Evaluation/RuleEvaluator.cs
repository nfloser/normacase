using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;

namespace NormaCase.RuleEngine.Evaluation;

public sealed class RuleEvaluator
{
    public AssessmentResult Evaluate(
        KnowledgePack pack,
        IReadOnlyDictionary<string, TruthValue> facts,
        DateOnly assessmentDate)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(facts);

        RejectUnknownCaseFields(pack, facts);

        var missingRequiredFields = pack.Fields
            .Where(field => field.Required)
            .Where(field => !facts.TryGetValue(field.Id, out var value)
                || value == TruthValue.Unknown)
            .Select(field => field.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var rule = ResolveEntryRule(pack, assessmentDate);
        if (rule is null)
        {
            return new(
                pack.Manifest.ReleaseId,
                assessmentDate,
                AssessmentOutcome.HumanReview,
                missingRequiredFields,
                null);
        }

        var condition = EvaluateCondition(rule.Condition, facts);
        var ruleOutcome = condition.Result switch
        {
            ConditionResult.Matched => rule.OnMatch,
            ConditionResult.NotMatched => rule.OnNoMatch,
            ConditionResult.Unknown => AssessmentOutcome.Incomplete,
            _ => AssessmentOutcome.Incomplete
        };

        var finalOutcome = missingRequiredFields.Length > 0
            ? AssessmentOutcome.Incomplete
            : ruleOutcome;

        var trace = new RuleTrace(
            rule.Id,
            rule.Version,
            rule.SourceId,
            condition.Result,
            finalOutcome,
            condition);

        return new(
            pack.Manifest.ReleaseId,
            assessmentDate,
            finalOutcome,
            missingRequiredFields,
            trace);
    }

    private static RuleDefinition? ResolveEntryRule(
        KnowledgePack pack,
        DateOnly assessmentDate)
    {
        var candidates = pack.Rules
            .Where(rule => string.Equals(
                rule.Id,
                pack.Manifest.EntryRuleId,
                StringComparison.Ordinal))
            .Where(rule => rule.ValidFrom <= assessmentDate)
            .Where(rule => rule.ValidUntil is null
                || assessmentDate <= rule.ValidUntil)
            .ToArray();

        return candidates.Length switch
        {
            0 => null,
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"Multiple active versions of entry rule '{pack.Manifest.EntryRuleId}' exist for {assessmentDate:yyyy-MM-dd}.")
        };
    }

    private static ConditionTrace EvaluateCondition(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, TruthValue> facts)
    {
        return condition.Kind switch
        {
            "field_equals" => EvaluateFieldEquals(condition, facts),
            "all" => EvaluateAll(condition, facts),
            "any" => EvaluateAny(condition, facts),
            _ => throw new InvalidOperationException(
                $"Knowledge validation should reject unknown condition kind '{condition.Kind}'.")
        };
    }

    private static ConditionTrace EvaluateFieldEquals(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, TruthValue> facts)
    {
        var actual = facts.TryGetValue(condition.Field!, out var value)
            ? value
            : TruthValue.Unknown;

        var result = actual == TruthValue.Unknown
            ? ConditionResult.Unknown
            : actual == condition.Expected
                ? ConditionResult.Matched
                : ConditionResult.NotMatched;

        return new(
            condition.Kind,
            result,
            condition.Field,
            condition.Expected,
            actual,
            []);
    }

    private static ConditionTrace EvaluateAll(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, TruthValue> facts)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts))
            .ToArray();

        var result = children.Any(child => child.Result == ConditionResult.NotMatched)
            ? ConditionResult.NotMatched
            : children.Any(child => child.Result == ConditionResult.Unknown)
                ? ConditionResult.Unknown
                : ConditionResult.Matched;

        return new(
            condition.Kind,
            result,
            null,
            null,
            null,
            children);
    }

    private static ConditionTrace EvaluateAny(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, TruthValue> facts)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts))
            .ToArray();

        var result = children.Any(child => child.Result == ConditionResult.Matched)
            ? ConditionResult.Matched
            : children.Any(child => child.Result == ConditionResult.Unknown)
                ? ConditionResult.Unknown
                : ConditionResult.NotMatched;

        return new(
            condition.Kind,
            result,
            null,
            null,
            null,
            children);
    }

    private static void RejectUnknownCaseFields(
        KnowledgePack pack,
        IReadOnlyDictionary<string, TruthValue> facts)
    {
        var knownFields = pack.Fields
            .Select(field => field.Id)
            .ToHashSet(StringComparer.Ordinal);

        var unknownFields = facts.Keys
            .Where(field => !knownFields.Contains(field))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknownFields.Length > 0)
        {
            throw new ArgumentException(
                $"Case contains fields not declared by the Knowledge Pack: {string.Join(", ", unknownFields)}",
                nameof(facts));
        }
    }
}
