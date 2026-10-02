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

        var domainOutputs = EvaluateDomainOutputs(
            pack,
            facts,
            evidence,
            assessmentDate);

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
                DomainOutputs = domainOutputs
            };
        }

        var condition = EvaluateCondition(rule.Condition, facts, evidence);
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

        var sourceTrace = CreateSourceTrace(pack, rule.SourceId);

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
            DomainOutputs = domainOutputs
        };
    }

    private static DomainOutputTrace[] EvaluateDomainOutputs(
        KnowledgePack pack,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence,
        DateOnly assessmentDate)
    {
        var traces = new List<DomainOutputTrace>();

        foreach (var group in pack.Outputs
                     .GroupBy(output => output.Id, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var candidates = group
                .Where(output => output.ValidFrom <= assessmentDate)
                .Where(output => output.ValidUntil is null
                    || assessmentDate <= output.ValidUntil)
                .ToArray();

            if (candidates.Length == 0)
            {
                continue;
            }

            if (candidates.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple active versions of domain output '{group.Key}' exist for {assessmentDate:yyyy-MM-dd}.");
            }

            var output = candidates[0];
            var condition = EvaluateCondition(output.Condition, facts, evidence);
            var value = condition.Result switch
            {
                ConditionResult.Matched => DomainOutputValue.FromChoice(output.OnMatch),
                ConditionResult.NotMatched => DomainOutputValue.FromChoice(output.OnNoMatch),
                ConditionResult.Unknown => DomainOutputValue.Unknown,
                _ => DomainOutputValue.Unknown
            };

            traces.Add(new(
                output.Id,
                output.Version,
                value,
                condition.Result,
                condition,
                CreateSourceTrace(pack, output.SourceId)));
        }

        return traces.ToArray();
    }

    private static SourceTrace CreateSourceTrace(
        KnowledgePack pack,
        string sourceId)
    {
        var source = pack.Sources.Single(item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));

        return new(
            source.Id,
            source.Version,
            source.SourceLocation,
            source.Authority,
            source.Title,
            source.DocumentType,
            source.Status,
            source.PublicationDate,
            source.ValidFrom,
            source.ValidUntil,
            source.RetrievedAt,
            source.ContentHash);
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
            "not" => EvaluateNot(condition, facts, evidence),
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
        var (actual, expressionTrace) = ResolveNumericInput(condition, facts);
        var expected = CaseValue.FromNumber(condition.Threshold!.Value);

        var result = actual.IsUnknown
            ? ConditionResult.Unknown
            : actual.Number >= condition.Threshold
                ? ConditionResult.Matched
                : ConditionResult.NotMatched;

        return LeafTrace(
            condition.Kind,
            result,
            condition.Field,
            expected,
            actual,
            expressionTrace);
    }

    private static ConditionTrace EvaluateNumberInRange(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var (actual, expressionTrace) = ResolveNumericInput(condition, facts);

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
            [],
            NumericExpression: expressionTrace);
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

    private static ConditionTrace EvaluateNot(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts,
        IReadOnlyDictionary<string, EvidenceStatus> evidence)
    {
        var child = EvaluateCondition(condition.Conditions[0], facts, evidence);
        var result = child.Result switch
        {
            ConditionResult.Matched => ConditionResult.NotMatched,
            ConditionResult.NotMatched => ConditionResult.Matched,
            _ => ConditionResult.Unknown
        };
        return GroupTrace(condition.Kind, result, [child]);
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

    private static (CaseValue Value, NumericExpressionTrace? Trace) ResolveNumericInput(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        if (condition.NumericExpression is null)
        {
            return (GetActualValue(condition, facts), null);
        }

        var trace = EvaluateNumericExpression(condition.NumericExpression, facts);
        return (trace.Value, trace);
    }

    private static NumericExpressionTrace EvaluateNumericExpression(
        NumericExpressionDefinition expression,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        return expression.Kind switch
        {
            "field" => EvaluateNumericFieldExpression(expression, facts),
            "range_lookup" => EvaluateNumericRangeLookup(expression, facts),
            "sum" => EvaluateNumericSum(expression, facts),
            "max" => EvaluateNumericMax(expression, facts),
            _ => throw new InvalidOperationException(
                $"Knowledge validation should reject unknown numeric expression kind '{expression.Kind}'.")
        };
    }

    private static NumericExpressionTrace EvaluateNumericFieldExpression(
        NumericExpressionDefinition expression,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var value = facts.TryGetValue(expression.Field!, out var supplied)
            ? supplied
            : CaseValue.Unknown;

        return new(
            expression.Kind,
            value,
            expression.Field,
            null,
            null,
            null,
            []);
    }

    private static NumericExpressionTrace EvaluateNumericRangeLookup(
        NumericExpressionDefinition expression,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var input = EvaluateNumericExpression(expression.Input!, facts);
        if (input.Value.IsUnknown)
        {
            return new(
                expression.Kind,
                CaseValue.Unknown,
                null,
                null,
                null,
                null,
                [input]);
        }

        var number = input.Value.Number!.Value;
        var band = expression.Bands.FirstOrDefault(item =>
            item.Minimum!.Value <= number
            && number <= item.Maximum!.Value);

        if (band is null)
        {
            return new(
                expression.Kind,
                CaseValue.Unknown,
                null,
                null,
                null,
                null,
                [input]);
        }

        var value = CaseValue.FromNumber(band.Value!.Value);
        return new(
            expression.Kind,
            value,
            null,
            band.Minimum,
            band.Maximum,
            band.Value,
            [input]);
    }

    private static NumericExpressionTrace EvaluateNumericSum(
        NumericExpressionDefinition expression,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var children = expression.Operands
            .Select(operand => EvaluateNumericExpression(operand, facts))
            .ToArray();

        var value = children.Any(child => child.Value.IsUnknown)
            ? CaseValue.Unknown
            : CaseValue.FromNumber(children.Sum(child => child.Value.Number!.Value));

        return new(
            expression.Kind,
            value,
            null,
            null,
            null,
            null,
            children);
    }

    private static NumericExpressionTrace EvaluateNumericMax(
        NumericExpressionDefinition expression,
        IReadOnlyDictionary<string, CaseValue> facts)
    {
        var children = expression.Operands
            .Select(operand => EvaluateNumericExpression(operand, facts))
            .ToArray();

        var value = children.Any(child => child.Value.IsUnknown)
            ? CaseValue.Unknown
            : CaseValue.FromNumber(children.Max(child => child.Value.Number!.Value));

        return new(
            expression.Kind,
            value,
            null,
            null,
            null,
            null,
            children);
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
        string? field,
        CaseValue expected,
        CaseValue actual,
        NumericExpressionTrace? numericExpression = null)
        => new(
            kind,
            result,
            field,
            expected,
            actual,
            null,
            null,
            [],
            NumericExpression: numericExpression);

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
