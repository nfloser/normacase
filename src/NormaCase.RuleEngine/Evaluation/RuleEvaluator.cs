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
        DateOnly assessmentDate,
        IReadOnlySet<string>? presentEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(facts);

        _validator.ValidateOrThrow(pack);
        ValidateCaseFields(pack, facts);

        var evidence = presentEvidence ?? EmptyEvidence.Instance;
        ValidateEvidence(pack, evidence);

        var evidenceRequirements = pack.EvidenceRequirements.ToDictionary(
            requirement => requirement.Id,
            StringComparer.Ordinal);

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
                [],
                null);
        }

        var condition = EvaluateCondition(
            rule.Condition,
            facts,
            evidenceRequirements,
            evidence);

        var missingEvidenceRequirements = FindMissingEvidence(condition);

        var ruleOutcome = condition.Result switch
        {
            ConditionResult.Matched => rule.OnMatch!.Value,
            ConditionResult.NotMatched => rule.OnNoMatch!.Value,
            ConditionResult.Unknown => ResolveUnknownOutcome(condition),
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
            missingEvidenceRequirements,
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
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidenceRequirements,
        IReadOnlySet<string> presentEvidence)
    {
        return condition.Kind switch
        {
            "field_equals" => EvaluateFieldEquals(condition, facts),
            "number_gte" => EvaluateNumberGte(condition, facts),
            "number_in_range" => EvaluateNumberInRange(condition, facts),
            "evidence_present" => EvaluateEvidencePresent(
                condition,
                evidenceRequirements,
                presentEvidence),
            "all" => EvaluateAll(
                condition,
                facts,
                evidenceRequirements,
                presentEvidence),
            "any" => EvaluateAny(
                condition,
                facts,
                evidenceRequirements,
                presentEvidence),
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
            null,
            null,
            []);
    }

    private static ConditionTrace EvaluateEvidencePresent(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidenceRequirements,
        IReadOnlySet<string> presentEvidence)
    {
        var requirement = evidenceRequirements[condition.EvidenceId!];
        var isPresent = presentEvidence.Contains(requirement.Id);

        return new(
            condition.Kind,
            isPresent ? ConditionResult.Matched : ConditionResult.Unknown,
            null,
            null,
            null,
            null,
            null,
            requirement.Id,
            isPresent ? null : requirement.MissingOutcome,
            []);
    }

    private static ConditionTrace EvaluateAll(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidenceRequirements,
        IReadOnlySet<string> presentEvidence)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(
                child,
                facts,
                evidenceRequirements,
                presentEvidence))
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
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidenceRequirements,
        IReadOnlySet<string> presentEvidence)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(
                child,
                facts,
                evidenceRequirements,
                presentEvidence))
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
            null,
            null,
            children);

    private static string[] FindMissingEvidence(ConditionTrace trace)
        => EnumerateTrace(trace)
            .Where(item => item.Kind == "evidence_present")
            .Where(item => item.Result == ConditionResult.Unknown)
            .Select(item => item.EvidenceId!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static AssessmentOutcome ResolveUnknownOutcome(ConditionTrace trace)
    {
        var missingOutcomes = EnumerateTrace(trace)
            .Where(item => item.Result == ConditionResult.Unknown)
            .Where(item => item.MissingEvidenceOutcome is not null)
            .Select(item => item.MissingEvidenceOutcome!.Value)
            .ToArray();

        return missingOutcomes.Contains(AssessmentOutcome.HumanReview)
            ? AssessmentOutcome.HumanReview
            : AssessmentOutcome.Incomplete;
    }

    private static IEnumerable<ConditionTrace> EnumerateTrace(
        ConditionTrace trace)
    {
        yield return trace;

        foreach (var child in trace.Children)
        {
            foreach (var descendant in EnumerateTrace(child))
            {
                yield return descendant;
            }
        }
    }

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

    private static void ValidateEvidence(
        KnowledgePack pack,
        IReadOnlySet<string> presentEvidence)
    {
        var declaredEvidence = pack.EvidenceRequirements
            .Select(requirement => requirement.Id)
            .ToHashSet(StringComparer.Ordinal);

        var undeclared = presentEvidence
            .Where(id => !declaredEvidence.Contains(id))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (undeclared.Length > 0)
        {
            throw new ArgumentException(
                $"Evidence contains requirements not declared by the Knowledge Pack: {string.Join(", ", undeclared)}",
                nameof(presentEvidence));
        }
    }

    private sealed class EmptyEvidence : IReadOnlySet<string>
    {
        public static readonly EmptyEvidence Instance = new();

        public int Count => 0;

        public bool Contains(string item) => false;
        public IEnumerator<string> GetEnumerator()
            => Enumerable.Empty<string>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<string> other) => other.Any();
        public bool IsProperSupersetOf(IEnumerable<string> other) => false;
        public bool IsSubsetOf(IEnumerable<string> other) => true;
        public bool IsSupersetOf(IEnumerable<string> other) => !other.Any();
        public bool Overlaps(IEnumerable<string> other) => false;
        public bool SetEquals(IEnumerable<string> other) => !other.Any();
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
