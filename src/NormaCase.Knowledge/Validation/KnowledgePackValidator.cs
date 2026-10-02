using NormaCase.Knowledge.Model;

namespace NormaCase.Knowledge.Validation;

public sealed class KnowledgePackValidator
{
    private static readonly HashSet<string> AllowedConditionKinds =
        new(StringComparer.Ordinal)
        {
            "all",
            "any",
            "field_equals"
        };

    private static readonly HashSet<string> AllowedValidationLevels =
        new(StringComparer.Ordinal)
        {
            "SYNTHETIC",
            "PUBLIC_REFERENCE",
            "DOMAIN_REVIEWED",
            "PRODUCTION_APPROVED"
        };

    public IReadOnlyList<KnowledgeValidationError> Validate(KnowledgePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var errors = new List<KnowledgeValidationError>();

        ValidateManifest(pack, errors);

        var fields = ValidateUniqueIds(
            pack.Fields,
            field => field.Id,
            "field",
            errors);

        foreach (var field in pack.Fields)
        {
            if (!string.Equals(field.Type, "truth", StringComparison.Ordinal))
            {
                errors.Add(new(
                    "unsupported_field_type",
                    $"Field '{field.Id}' uses unsupported type '{field.Type}'.",
                    $"fields.{field.Id}.type"));
            }
        }

        var sources = ValidateUniqueIds(
            pack.Sources,
            source => source.Id,
            "source",
            errors);

        foreach (var source in pack.Sources)
        {
            Require(source.Authority, $"sources.{source.Id}.authority", errors);
            Require(source.Title, $"sources.{source.Id}.title", errors);
            Require(source.DocumentType, $"sources.{source.Id}.documentType", errors);
        }

        var rules = ValidateRules(pack, fields, sources, errors);

        if (!string.IsNullOrWhiteSpace(pack.Manifest.EntryRuleId)
            && !rules.Any(rule => string.Equals(
                rule.Id,
                pack.Manifest.EntryRuleId,
                StringComparison.Ordinal)))
        {
            errors.Add(new(
                "missing_entry_rule",
                $"Entry rule '{pack.Manifest.EntryRuleId}' does not exist.",
                "manifest.entryRuleId"));
        }

        ValidateTemporalOverlaps(rules, errors);

        return errors;
    }

    public void ValidateOrThrow(KnowledgePack pack)
    {
        var errors = Validate(pack);
        if (errors.Count > 0)
        {
            throw new KnowledgeValidationException(errors);
        }
    }

    private static void ValidateManifest(
        KnowledgePack pack,
        ICollection<KnowledgeValidationError> errors)
    {
        Require(pack.Manifest.PackId, "manifest.packId", errors);
        Require(pack.Manifest.ReleaseId, "manifest.releaseId", errors);
        Require(pack.Manifest.ValidationLevel, "manifest.validationLevel", errors);
        Require(pack.Manifest.EntryRuleId, "manifest.entryRuleId", errors);

        if (!string.IsNullOrWhiteSpace(pack.Manifest.ValidationLevel)
            && !AllowedValidationLevels.Contains(pack.Manifest.ValidationLevel))
        {
            errors.Add(new(
                "unknown_validation_level",
                $"Unknown validation level '{pack.Manifest.ValidationLevel}'.",
                "manifest.validationLevel"));
        }
    }

    private static IReadOnlyDictionary<string, T> ValidateUniqueIds<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        string itemName,
        ICollection<KnowledgeValidationError> errors)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add(new(
                    $"missing_{itemName}_id",
                    $"{itemName} id is required.",
                    $"{itemName}s"));
                continue;
            }

            if (!result.TryAdd(id, item))
            {
                errors.Add(new(
                    $"duplicate_{itemName}_id",
                    $"Duplicate {itemName} id '{id}'.",
                    $"{itemName}s.{id}"));
            }
        }

        return result;
    }

    private static List<RuleDefinition> ValidateRules(
        KnowledgePack pack,
        IReadOnlyDictionary<string, FieldDefinition> fields,
        IReadOnlyDictionary<string, SourceDefinition> sources,
        ICollection<KnowledgeValidationError> errors)
    {
        var rules = new List<RuleDefinition>();
        var identities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in pack.Rules)
        {
            rules.Add(rule);
            var path = $"rules.{rule.Id}@{rule.Version}";

            Require(rule.Id, $"{path}.id", errors);

            if (rule.Version <= 0)
            {
                errors.Add(new(
                    "invalid_rule_version",
                    $"Rule '{rule.Id}' must have a positive version.",
                    $"{path}.version"));
            }

            var identity = $"{rule.Id}@{rule.Version}";
            if (!identities.Add(identity))
            {
                errors.Add(new(
                    "duplicate_rule_version",
                    $"Duplicate rule version '{identity}'.",
                    path));
            }

            if (rule.ValidUntil is not null && rule.ValidUntil < rule.ValidFrom)
            {
                errors.Add(new(
                    "invalid_validity_interval",
                    $"Rule '{identity}' ends before it starts.",
                    path));
            }

            if (string.IsNullOrWhiteSpace(rule.SourceId)
                || !sources.ContainsKey(rule.SourceId))
            {
                errors.Add(new(
                    "missing_source",
                    $"Rule '{identity}' references unknown source '{rule.SourceId}'.",
                    $"{path}.sourceId"));
            }

            if (rule.OnMatch is null)
            {
                errors.Add(new(
                    "missing_on_match",
                    $"Rule '{identity}' must define onMatch.",
                    $"{path}.onMatch"));
            }

            if (rule.OnNoMatch is null)
            {
                errors.Add(new(
                    "missing_on_no_match",
                    $"Rule '{identity}' must define onNoMatch.",
                    $"{path}.onNoMatch"));
            }

            ValidateCondition(rule.Condition, fields, errors, $"{path}.condition");
        }

        return rules;
    }

    private static void ValidateCondition(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, FieldDefinition> fields,
        ICollection<KnowledgeValidationError> errors,
        string path)
    {
        if (!AllowedConditionKinds.Contains(condition.Kind))
        {
            errors.Add(new(
                "unknown_condition_kind",
                $"Unknown condition kind '{condition.Kind}'.",
                $"{path}.kind"));
            return;
        }

        if (condition.Kind is "all" or "any")
        {
            if (condition.Conditions.Count == 0)
            {
                errors.Add(new(
                    "empty_condition_group",
                    $"Condition group '{condition.Kind}' must contain at least one child.",
                    $"{path}.conditions"));
                return;
            }

            for (var index = 0; index < condition.Conditions.Count; index++)
            {
                ValidateCondition(
                    condition.Conditions[index],
                    fields,
                    errors,
                    $"{path}.conditions[{index}]");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(condition.Field)
            || !fields.ContainsKey(condition.Field))
        {
            errors.Add(new(
                "missing_field",
                $"Condition references unknown field '{condition.Field}'.",
                $"{path}.field"));
        }

        if (condition.Expected is null)
        {
            errors.Add(new(
                "missing_expected_value",
                "field_equals requires an expected truth value.",
                $"{path}.expected"));
        }

        if (condition.Conditions.Count > 0)
        {
            errors.Add(new(
                "leaf_has_children",
                "field_equals cannot contain child conditions.",
                $"{path}.conditions"));
        }
    }

    private static void ValidateTemporalOverlaps(
        IEnumerable<RuleDefinition> rules,
        ICollection<KnowledgeValidationError> errors)
    {
        foreach (var group in rules.GroupBy(rule => rule.Id, StringComparer.Ordinal))
        {
            var ordered = group
                .OrderBy(rule => rule.ValidFrom)
                .ThenBy(rule => rule.Version)
                .ToArray();

            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];

                if (previous.ValidUntil is null
                    || previous.ValidUntil >= current.ValidFrom)
                {
                    errors.Add(new(
                        "overlapping_rule_validity",
                        $"Rule '{group.Key}' has overlapping versions {previous.Version} and {current.Version}.",
                        $"rules.{group.Key}"));
                }
            }
        }
    }

    private static void Require(
        string value,
        string path,
        ICollection<KnowledgeValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new(
                "required_value_missing",
                $"Required value '{path}' is missing.",
                path));
        }
    }
}
