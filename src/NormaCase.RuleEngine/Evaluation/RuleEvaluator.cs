using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;
using NormaCase.Knowledge.Validation;

namespace NormaCase.RuleEngine.Evaluation;

public sealed class RuleEvaluator
{
    private readonly KnowledgePackValidator _validator;

    public RuleEvaluator(KnowledgePackValidator? validator = null)
    {
        _validator = validator ?? new KnowledgePackValidator();
    }

    public AssessmentResult Evaluate(
        KnowledgePack pack,
        IReadOnlyDictionary<string, CaseValue> facts,
        DateOnly assessmentDate)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(facts);

        _validator.ValidateOrThrow(pack);
        ValidateCaseFields(pack, facts);

        var missingRequiredFields = pack.Fields
            .Where(field => field.Required)
            .Where(field => !facts.TryGetValue(field.Id, out var value)
                || value.IsUnknown)
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
            ConditionResult.Matched => rule.OnMatch!.Value,
            ConditionResult.NotMatched => rule.OnNoMatch!.Value,
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
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        return condition.Kind switch
        {
            "field_equals" => EvaluateFieldEquals(condition, facts),
            "number_gte" => EvaluateNumberGte(condition, facts),
            "number_in_range" => EvaluateNumberInRange(condition, facts),
            "all" => EvaluateAll(condition, facts),
            "any" => EvaluateAny(condition, facts),
            _ => throw new InvalidOperationException(
                $"Knowledge validation should reject unknown condition kind '{condition.Kind}'.")
        };
    }

    private static ConditionTrace EvaluateFieldEquals(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var actual = GetActualValue(condition, facts);
        var expected = CaseValue.FromTruth(condition.Expected!.Value);

        var result = actual.IsUnknown
            ? ConditionResult.Unknown
            : actual.Truth == condition.Expected
                ? ConditionResult.Matched
                : ConditionResult.NotMatched;

        return LeafTrace(
            condition.Kind,
            result,
            condition.Field!,
            expected,
            actual);
    }

    private static ConditionTrace EvaluateNumberGte(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var actual = GetActualValue(condition, facts);
        var expected = CaseValue.FromNumber(condition.Threshold!.Value);

        var result = actual.IsUnknown
            ? ConditionResult.Unknown
            : actual.Number >= condition.Threshold
                ? ConditionResult.Matched
                : ConditionResult.NotMatched;

        return LeafTrace(
            condition.Kind,
            result,
            condition.Field!,
            expected,
            actual);
    }

    private static ConditionTrace EvaluateNumberInRange(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var actual = GetActualValue(condition, facts);

        var result = actual.IsUnknown
            ? ConditionResult.Unknown
            : actual.Number >= condition.Minimum
                && actual.Number <= condition.Maximum
                    ? ConditionResult.Matched
                    : ConditionResult.NotMatched;

        return new(
            condition.Kind,
            result,
            condition.Field,
            null,
            actual,
            condition.Minimum,
            condition.Maximum,
            []);
    }

    private static ConditionTrace EvaluateAll(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts))
            .ToArray();

        var result = children.Any(child => child.Result == ConditionResult.NotMatched)
            ? ConditionResult.NotMatched
            : children.Any(child => child.Result == ConditionResult.Unknown)
                ? ConditionResult.Unknown
                : ConditionResult.Matched;

        return GroupTrace(condition.Kind, result, children);
    }

    private static ConditionTrace EvaluateAny(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts))
            .ToArray();

        var result = children.Any(child => child.Result == ConditionResult.Matched)
            ? ConditionResult.Matched
            : children.Any(child => child.Result == ConditionResult.Unknown)
                ? ConditionResult.Unknown
                : ConditionResult.NotMatched;

        return GroupTrace(condition.Kind, result, children);
    }

    private static ConditionTrace LeafTrace(
        string kind,
        ConditionResult result,
        string field,
        CaseValue expected,
        CaseValue actual)
        => new(
            kind,
            result,
            field,
            expected,
            actual,
            null,
            null,
            []);

    private static ConditionTrace GroupTrace(
        string kind,
        ConditionResult result,
        IReadOnlyList<ConditionTrace> children)
        => new(
            kind,
            result,
            null,
            null,
            null,
            null,
            null,
            children);

    private static CaseValue GetActualValue(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
        => facts.TryGetValue(condition.Field!, out var value)
            ? value
            : CaseValue.Unknown;

    private static void ValidateCaseFields(
        KnowledgePack pack,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var fields = pack.Fields.ToDictionary(
            field => field.Id,
            StringComparer.Ordinal);

        var unknownFields = facts.Keys
            .Where(field => !fields.ContainsKey(field))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknownFields.Length > 0)
        {
            throw new ArgumentException(
                $"Case contains fields not declared by the Knowledge Pack: {string.Join(", ", unknownFields)}",
                nameof(facts));
        }

        var typeMismatches = facts
            .Where(pair => !pair.Value.IsUnknown)
            .Where(pair => !ValueMatchesFieldType(pair.Value, fields[pair.Key].Type))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (typeMismatches.Length > 0)
        {
            throw new ArgumentException(
                $"Case values do not match their declared field types: {string.Join(", ", typeMismatches)}",
                nameof(facts));
        }
    }

    private static bool ValueMatchesFieldType(
        CaseValue value,
        string fieldType)
        => fieldType switch
        {
            "truth" => value.Kind == CaseValueKind.Truth,
            "number" => value.Kind == CaseValueKind.Number,
            _ => false
        };
}
