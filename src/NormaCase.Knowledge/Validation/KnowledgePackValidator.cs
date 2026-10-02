using NormaCase.Domain.Decision;
using NormaCase.Knowledge.Model;

namespace NormaCase.Knowledge.Validation;

public sealed class KnowledgePackValidator
{
    private static readonly HashSet<string> AllowedConditionKinds =
        new(StringComparer.Ordinal)
        {
            "all",
            "any",
            "field_equals",
            "number_gte",
            "number_in_range",
            "requires_evidence"
        };

    private static readonly HashSet<string> AllowedFieldTypes =
        new(StringComparer.Ordinal)
        {
            "truth",
            "number"
        };

    private static readonly HashSet<string> AllowedValidationLevels =
        new(StringComparer.Ordinal)
        {
            "SYNTHETIC",
            "PUBLIC_REFERENCE",
            "DOMAIN_REVIEWED",
            "PRODUCTION_APPROVED"
        };

    private static readonly HashSet<string> AllowedLifecycleStatuses =
        new(StringComparer.Ordinal)
        {
            "DRAFT",
            "IN_REVIEW",
            "APPROVED",
            "ACTIVE",
            "DEPRECATED",
            "RETIRED"
        };

    private const int CurrentFormatVersion = 1;

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
            if (!AllowedFieldTypes.Contains(field.Type))
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
            ValidateSource(
                source,
                RequiresPublicProvenance(pack.Manifest.ValidationLevel),
                errors);
        }

        var evidence = ValidateUniqueIds(pack.EvidenceRequirements, item => item.Id, "evidence_requirement", errors);
        var rules = ValidateRules(pack, fields, sources, evidence, errors);

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
        if (pack.Manifest.FormatVersion != CurrentFormatVersion)
        {
            errors.Add(new(
                "unsupported_format_version",
                $"Knowledge Pack format version '{pack.Manifest.FormatVersion}' is not supported. Expected '{CurrentFormatVersion}'.",
                "manifest.formatVersion"));
        }

        Require(pack.Manifest.PackId, "manifest.packId", errors);
        Require(pack.Manifest.ReleaseId, "manifest.releaseId", errors);
        Require(pack.Manifest.LifecycleStatus, "manifest.lifecycleStatus", errors);
        Require(pack.Manifest.ValidationLevel, "manifest.validationLevel", errors);
        Require(pack.Manifest.EntryRuleId, "manifest.entryRuleId", errors);

        if (!string.IsNullOrWhiteSpace(pack.Manifest.LifecycleStatus)
            && !AllowedLifecycleStatuses.Contains(pack.Manifest.LifecycleStatus))
        {
            errors.Add(new(
                "unknown_lifecycle_status",
                $"Unknown lifecycle status '{pack.Manifest.LifecycleStatus}'.",
                "manifest.lifecycleStatus"));
        }

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
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidence,
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

            if (rule.OnUnknown is not null
                && rule.OnUnknown is not (AssessmentOutcome.Incomplete or AssessmentOutcome.HumanReview))
            {
                errors.Add(new("unsafe_on_unknown", "onUnknown must be INCOMPLETE or HUMAN_REVIEW.", $"{path}.onUnknown"));
            }

            ValidateCondition(rule.Condition, fields, evidence, errors, $"{path}.condition");
        }

        return rules;
    }

    private static void ValidateCondition(
        ConditionDefinition condition,
        IReadOnlyDictionary<string, FieldDefinition> fields,
        IReadOnlyDictionary<string, EvidenceRequirementDefinition> evidence,
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

        if (condition.Kind == "requires_evidence")
        {
            if (condition.Field is not null || condition.Expected is not null
                || condition.Threshold is not null || condition.Minimum is not null || condition.Maximum is not null)
            {
                errors.Add(new("ambiguous_evidence_dependency", "Evidence gates cannot also declare field comparisons.", path));
            }

            if (string.IsNullOrWhiteSpace(condition.EvidenceRequirementId)
                || !evidence.ContainsKey(condition.EvidenceRequirementId))
            {
                errors.Add(new("missing_evidence_requirement", "Condition references undeclared evidence.", $"{path}.evidenceRequirementId"));
            }

            if (condition.Conditions.Count != 1)
            {
                errors.Add(new("invalid_evidence_dependency", "requires_evidence must contain exactly one condition.", $"{path}.conditions"));
            }

            foreach (var child in condition.Conditions)
            {
                ValidateCondition(child, fields, evidence, errors, $"{path}.conditions");
            }

            return;
        }

        if (condition.EvidenceRequirementId is not null)
        {
            errors.Add(new("unexpected_evidence_reference", "Only requires_evidence can reference evidence.", $"{path}.evidenceRequirementId"));
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
                    evidence,
                    errors,
                    $"{path}.conditions[{index}]");
            }

            return;
        }

        FieldDefinition? field = null;
        if (string.IsNullOrWhiteSpace(condition.Field)
            || !fields.TryGetValue(condition.Field, out field))
        {
            errors.Add(new(
                "missing_field",
                $"Condition references unknown field '{condition.Field}'.",
                $"{path}.field"));
        }

        if (condition.Conditions.Count > 0)
        {
            errors.Add(new(
                "leaf_has_children",
                $"Condition '{condition.Kind}' cannot contain child conditions.",
                $"{path}.conditions"));
        }

        switch (condition.Kind)
        {
            case "field_equals":
                ValidateTruthEquality(condition, field, errors, path);
                break;

            case "number_gte":
                ValidateNumericThreshold(condition, field, errors, path);
                break;

            case "number_in_range":
                ValidateNumericRange(condition, field, errors, path);
                break;
        }
    }

    private static void ValidateTruthEquality(
        ConditionDefinition condition,
        FieldDefinition? field,
        ICollection<KnowledgeValidationError> errors,
        string path)
    {
        if (condition.Expected is null)
        {
            errors.Add(new(
                "missing_expected_value",
                "field_equals requires an expected truth value.",
                $"{path}.expected"));
        }
        else if (condition.Expected == TruthValue.Unknown)
        {
            errors.Add(new(
                "unknown_expected_value",
                "field_equals cannot use UNKNOWN as an expected value.",
                $"{path}.expected"));
        }

        ValidateFieldType(field, "truth", condition.Kind, errors, path);
    }

    private static void ValidateNumericThreshold(
        ConditionDefinition condition,
        FieldDefinition? field,
        ICollection<KnowledgeValidationError> errors,
        string path)
    {
        if (condition.Threshold is null)
        {
            errors.Add(new(
                "missing_threshold",
                "number_gte requires a threshold.",
                $"{path}.threshold"));
        }

        ValidateFieldType(field, "number", condition.Kind, errors, path);
    }

    private static void ValidateNumericRange(
        ConditionDefinition condition,
        FieldDefinition? field,
        ICollection<KnowledgeValidationError> errors,
        string path)
    {
        if (condition.Minimum is null)
        {
            errors.Add(new(
                "missing_minimum",
                "number_in_range requires a minimum.",
                $"{path}.minimum"));
        }

        if (condition.Maximum is null)
        {
            errors.Add(new(
                "missing_maximum",
                "number_in_range requires a maximum.",
                $"{path}.maximum"));
        }

        if (condition.Minimum is not null
            && condition.Maximum is not null
            && condition.Minimum > condition.Maximum)
        {
            errors.Add(new(
                "invalid_numeric_range",
                "number_in_range minimum cannot exceed maximum.",
                path));
        }

        ValidateFieldType(field, "number", condition.Kind, errors, path);
    }

    private static void ValidateFieldType(
        FieldDefinition? field,
        string expectedType,
        string conditionKind,
        ICollection<KnowledgeValidationError> errors,
        string path)
    {
        if (field is not null
            && !string.Equals(field.Type, expectedType, StringComparison.Ordinal))
        {
            errors.Add(new(
                "condition_field_type_mismatch",
                $"Condition '{conditionKind}' requires field type '{expectedType}', but '{field.Id}' is '{field.Type}'.",
                $"{path}.field"));
        }
    }

    private static void ValidateSource(
        SourceDefinition source,
        bool requirePublicProvenance,
        ICollection<KnowledgeValidationError> errors)
    {
        var path = $"sources.{source.Id}";

        Require(source.Authority, $"{path}.authority", errors);
        Require(source.Title, $"{path}.title", errors);
        Require(source.DocumentType, $"{path}.documentType", errors);
        Require(source.Status, $"{path}.status", errors);
        Require(source.Version ?? string.Empty, $"{path}.version", errors);
        Require(source.SourceLocation ?? string.Empty, $"{path}.sourceLocation", errors);

        if (source.ValidFrom is not null
            && source.ValidUntil is not null
            && source.ValidUntil < source.ValidFrom)
        {
            errors.Add(new(
                "invalid_source_validity_interval",
                $"Source '{source.Id}' ends before it starts.",
                path));
        }

        if (!string.IsNullOrWhiteSpace(source.ContentHash)
            && !IsSha256(source.ContentHash))
        {
            errors.Add(new(
                "invalid_source_content_hash",
                $"Source '{source.Id}' contentHash must use 'sha256:' followed by 64 hexadecimal characters.",
                $"{path}.contentHash"));
        }

        if (!requirePublicProvenance)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(source.SourceLocation))
        {
            errors.Add(new(
                "missing_source_location",
                $"Source '{source.Id}' requires sourceLocation for this validation level.",
                $"{path}.sourceLocation"));
        }

        if (source.RetrievedAt is null)
        {
            errors.Add(new(
                "missing_source_retrieved_at",
                $"Source '{source.Id}' requires retrievedAt for this validation level.",
                $"{path}.retrievedAt"));
        }

        if (string.IsNullOrWhiteSpace(source.ContentHash))
        {
            errors.Add(new(
                "missing_source_content_hash",
                $"Source '{source.Id}' requires contentHash for this validation level.",
                $"{path}.contentHash"));
        }
    }

    private static bool RequiresPublicProvenance(string validationLevel)
        => validationLevel is
            "PUBLIC_REFERENCE"
            or "DOMAIN_REVIEWED"
            or "PRODUCTION_APPROVED";

    private static bool IsSha256(string value)
    {
        const string prefix = "sha256:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length != prefix.Length + 64)
        {
            return false;
        }

        return value[prefix.Length..].All(Uri.IsHexDigit);
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
