using NormaCase.Domain.Cases;
using NormaCase.Domain.Decision;
using NormaCase.Domain.Evidence;
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
        IReadOnlyDictionary<string, EvidenceStatus>? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(facts);

        _validator.ValidateOrThrow(pack);
        ValidateCaseFields(pack, facts);
        evidence ??= new Dictionary<string, EvidenceStatus>(StringComparer.Ordinal);
        ValidateEvidence(pack, evidence);

        var missingRequiredFields = pack.Fields
            .Where(field => field.Required)
            .Where(field => !facts.TryGetValue(field.Id, out var value)
                || value.IsUnknown)
            .Select(field => field.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var evaluationFacts = new Dictionary<string, CaseValue>(
            facts,
            StringComparer.Ordinal);
        var calculationTraces = EvaluateCalculations(
            pack.Calculations,
            evaluationFacts);

        var rule = ResolveEntryRule(pack, assessmentDate);
        if (rule is null)
        {
            return new(
                pack.Manifest.ReleaseId,
                assessmentDate,
                AssessmentOutcome.HumanReview,
                missingRequiredFields,
                null)
            {
                Calculations = calculationTraces
            };
        }

        var condition = EvaluateCondition(
            rule.Condition,
            evaluationFacts,
            evidence);
        var ruleOutcome = condition.Result switch
        {
            ConditionResult.Matched => rule.OnMatch!.Value,
            ConditionResult.NotMatched => rule.OnNoMatch!.Value,
            ConditionResult.Unknown => rule.OnUnknown ?? AssessmentOutcome.Incomplete,
            _ => AssessmentOutcome.Incomplete
        };

        var finalOutcome = missingRequiredFields.Length > 0
            ? AssessmentOutcome.Incomplete
            : HasConflictingEvidence(condition)
                ? AssessmentOutcome.HumanReview
                : ruleOutcome;

        var source = pack.Sources.Single(item => string.Equals(item.Id, rule.SourceId, StringComparison.Ordinal));
        var sourceTrace = new SourceTrace(
            source.Id, source.Version, source.SourceLocation, source.Authority, source.Title,
            source.DocumentType, source.Status, source.PublicationDate, source.ValidFrom,
            source.ValidUntil, source.RetrievedAt, source.ContentHash);

        var trace = new RuleTrace(
            rule.Id,
            rule.Version,
            rule.SourceId,
            condition.Result,
            finalOutcome,
            condition,
            sourceTrace);

        return new(
            pack.Manifest.ReleaseId,
            assessmentDate,
            finalOutcome,
            missingRequiredFields,
            trace)
        {
            Calculations = calculationTraces
        };
    }

    private static IReadOnlyList<CalculationTrace> EvaluateCalculations(
        IReadOnlyList<CalculationDefinition> calculations,
        IDictionary<string, CaseValue> evaluationFacts)
    {
        var traces = new List<CalculationTrace>(calculations.Count);

        foreach (var calculation in calculations)
        {
            var trace = calculation.Kind switch
            {
                "range_lookup" => EvaluateRangeLookup(
                    calculation,
                    evaluationFacts),
                "sum" => EvaluateSum(
                    calculation,
                    evaluationFacts),
                "max" => EvaluateMax(
                    calculation,
                    evaluationFacts),
                _ => throw new InvalidOperationException(
                    $"Knowledge validation should reject unknown calculation kind '{calculation.Kind}'.")
            };

            evaluationFacts.Add(calculation.Id, trace.Result);
            traces.Add(trace);
        }

        return traces;
    }

    private static CalculationTrace EvaluateRangeLookup(
        CalculationDefinition calculation,
        IReadOnlyDictionary<string, CaseValue> evaluationFacts)
    {
        var inputValue = GetCalculationInput(
            calculation.Input!,
            evaluationFacts);
        var inputs = new[]
        {
            new CalculationInputTrace(
                calculation.Input!,
                inputValue)
        };

        if (inputValue.IsUnknown)
        {
            return new(
                calculation.Id,
                calculation.Kind,
                inputs,
                CaseValue.Unknown,
                null);
        }

        var number = inputValue.Number!.Value;
        var matches = calculation.Ranges
            .Where(range => IsInLookupRange(number, range))
            .ToArray();

        if (matches.Length == 0)
        {
            return new(
                calculation.Id,
                calculation.Kind,
                inputs,
                CaseValue.Unknown,
                null);
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Validated range_lookup '{calculation.Id}' matched more than one range.");
        }

        var selected = matches[0];
        var result = CaseValue.FromNumber(selected.Value!.Value);
        var rangeTrace = new RangeLookupTrace(
            selected.Minimum!.Value,
            selected.MinimumInclusive,
            selected.Maximum!.Value,
            selected.MaximumInclusive,
            selected.Value.Value);

        return new(
            calculation.Id,
            calculation.Kind,
            inputs,
            result,
            rangeTrace);
    }

    private static CalculationTrace EvaluateSum(
        CalculationDefinition calculation,
        IReadOnlyDictionary<string, CaseValue> evaluationFacts)
    {
        var inputs = GetCalculationInputs(
            calculation.Inputs,
            evaluationFacts);

        if (inputs.Any(input => input.Value.IsUnknown))
        {
            return new(
                calculation.Id,
                calculation.Kind,
                inputs,
                CaseValue.Unknown,
                null);
        }

        var sum = inputs.Aggregate(
            0m,
            (current, input) => checked(
                current + input.Value.Number!.Value));

        return new(
            calculation.Id,
            calculation.Kind,
            inputs,
            CaseValue.FromNumber(sum),
            null);
    }

    private static CalculationTrace EvaluateMax(
        CalculationDefinition calculation,
        IReadOnlyDictionary<string, CaseValue> evaluationFacts)
    {
        var inputs = GetCalculationInputs(
            calculation.Inputs,
            evaluationFacts);

        if (inputs.Any(input => input.Value.IsUnknown))
        {
            return new(
                calculation.Id,
                calculation.Kind,
                inputs,
                CaseValue.Unknown,
                null);
        }

        var maximum = inputs
            .Max(input => input.Value.Number!.Value);

        return new(
            calculation.Id,
            calculation.Kind,
            inputs,
            CaseValue.FromNumber(maximum),
            null);
    }

    private static CalculationInputTrace[] GetCalculationInputs(
        IEnumerable<string> inputIds,
        IReadOnlyDictionary<string, CaseValue> evaluationFacts)
        => inputIds
            .Select(id => new CalculationInputTrace(
                id,
                GetCalculationInput(id, evaluationFacts)))
            .ToArray();

    private static CaseValue GetCalculationInput(
        string inputId,
        IReadOnlyDictionary<string, CaseValue> evaluationFacts)
        => evaluationFacts.TryGetValue(inputId, out var value)
            ? value
            : CaseValue.Unknown;

    private static bool IsInLookupRange(
        decimal value,
        RangeLookupDefinition range)
    {
        var lowerMatches = range.MinimumInclusive
            ? value >= range.Minimum
            : value > range.Minimum;
        var upperMatches = range.MaximumInclusive
            ? value <= range.Maximum
            : value < range.Maximum;

        return lowerMatches && upperMatches;
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
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        return condition.Kind switch
        {
            "field_equals" => EvaluateFieldEquals(condition, facts),
            "number_gte" => EvaluateNumberGte(condition, facts),
            "number_in_range" => EvaluateNumberInRange(condition, facts),
            "all" => EvaluateAll(condition, facts, evidence),
            "any" => EvaluateAny(condition, facts, evidence),
            "requires_evidence" => EvaluateEvidenceDependency(condition, facts, evidence),
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
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts, evidence))
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
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        var children = condition.Conditions
            .Select(child => EvaluateCondition(child, facts, evidence))
            .ToArray();

        var result = children.Any(child => child.Result == ConditionResult.Matched)
            ? ConditionResult.Matched
            : children.Any(child => child.Result == ConditionResult.Unknown)
                ? ConditionResult.Unknown
                : ConditionResult.NotMatched;

        return GroupTrace(condition.Kind, result, children);
    }

    private static ConditionTrace EvaluateEvidenceDependency(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        var status = evidence.TryGetValue(condition.EvidenceRequirementId!, out var supplied)
            ? supplied
            : EvidenceStatus.Missing;
        var child = EvaluateCondition(condition.Conditions[0], facts, evidence);
        var result = status == EvidenceStatus.Present ? child.Result : ConditionResult.Unknown;
        return new(condition.Kind, result, null, null, null, null, null, [child],
            condition.EvidenceRequirementId, status);
    }

    private static bool HasConflictingEvidence(ConditionTrace trace)
        => trace.EvidenceStatus == EvidenceStatus.Conflicting
            || trace.Children.Any(HasConflictingEvidence);

    private static void ValidateEvidence(
        KnowledgePack pack,
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        var declared = pack.EvidenceRequirements.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (evidence.Any(item => !declared.Contains(item.Key) || !Enum.IsDefined(item.Value)))
        {
            throw new ArgumentException("Evidence contains an undeclared requirement or invalid status.", nameof(evidence));
        }
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
