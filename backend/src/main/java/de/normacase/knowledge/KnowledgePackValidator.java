package de.normacase.knowledge;

import de.normacase.knowledge.KnowledgePack.Condition;
import de.normacase.knowledge.KnowledgePack.Field;
import de.normacase.knowledge.KnowledgePack.NumericExpression;
import de.normacase.knowledge.KnowledgePack.Output;
import de.normacase.knowledge.KnowledgePack.RangeBand;
import de.normacase.knowledge.KnowledgePack.Rule;
import de.normacase.knowledge.KnowledgePack.Source;
import de.normacase.knowledge.KnowledgePack.Workflow;
import de.normacase.knowledge.KnowledgeValidationException.Error;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.function.Function;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

/**
 * Strukturelle und zeitliche Prüfung eines Knowledge Packs. Fehlercodes entsprechen dem Referenzstand,
 * damit ungültige Regelwerke in beiden Implementierungen gleich abgelehnt werden.
 */
public final class KnowledgePackValidator {

    static final int FORMAT_VERSION = 1;
    static final Set<String> FIELD_TYPES = Set.of("truth", "number");
    static final Set<String> OUTPUT_TYPES = Set.of("choice", "number");
    static final Set<String> VALIDATION_LEVELS = Set.of("SYNTHETIC", "PUBLIC_REFERENCE", "DOMAIN_REVIEWED", "PRODUCTION_APPROVED");
    static final Set<String> LIFECYCLE = Set.of("DRAFT", "IN_REVIEW", "APPROVED", "ACTIVE", "DEPRECATED", "RETIRED");
    static final Set<String> LEAF_CONDITIONS = Set.of("field_equals", "number_gte", "number_in_range");
    static final Set<String> GROUP_CONDITIONS = Set.of("all", "any", "not", "requires_evidence");
    static final Set<String> NUMERIC_KINDS = Set.of("field", "range_lookup", "sum", "max", "scale");
    private static final Pattern SHA256 = Pattern.compile("sha256:[0-9a-fA-F]{64}");

    public void validateOrThrow(KnowledgePack pack) {
        List<Error> errors = validate(pack);
        if (!errors.isEmpty()) {
            throw new KnowledgeValidationException(errors);
        }
    }

    public List<Error> validate(KnowledgePack pack) {
        Context c = new Context(pack);
        manifest(c);
        fields(c);
        sources(c);
        rules(c);
        outputs(c);
        workflows(c);
        return List.copyOf(c.errors);
    }

    private static final class Context {
        final KnowledgePack pack;
        final List<Error> errors = new ArrayList<>();
        final Map<String, Field> fields = new HashMap<>();
        final Set<String> sources = new HashSet<>();
        final Set<String> evidence = new HashSet<>();

        Context(KnowledgePack pack) {
            this.pack = pack;
            pack.fields().forEach(f -> fields.putIfAbsent(f.id(), f));
            pack.sources().forEach(s -> sources.add(s.id()));
            pack.evidenceRequirements().forEach(e -> evidence.add(e.id()));
        }

        void add(String code, String message, String path) {
            errors.add(new Error(code, message, path));
        }
    }

    private static void manifest(Context c) {
        var m = c.pack.manifest();
        if (m.formatVersion() != FORMAT_VERSION) {
            c.add("unsupported_format_version", "Unsupported Knowledge Pack format.", "$.manifest.formatVersion");
        }
        required(c, m.packId(), "$.manifest.packId");
        required(c, m.releaseId(), "$.manifest.releaseId");
        required(c, m.entryRuleId(), "$.manifest.entryRuleId");
        if (!VALIDATION_LEVELS.contains(m.validationLevel())) {
            c.add("unknown_validation_level", "Unknown validation level.", "$.manifest.validationLevel");
        }
        if (!LIFECYCLE.contains(m.lifecycleStatus())) {
            c.add("unknown_lifecycle_status", "Unknown lifecycle status.", "$.manifest.lifecycleStatus");
        }
        if (c.pack.rules().stream().noneMatch(r -> r.id().equals(m.entryRuleId()))) {
            c.add("missing_entry_rule", "Entry rule is not defined.", "$.manifest.entryRuleId");
        }
    }

    private static void fields(Context c) {
        duplicates(c, c.pack.fields(), Field::id, "$.fields", "duplicate_field");
        duplicates(c, c.pack.evidenceRequirements(), KnowledgePack.EvidenceRequirement::id, "$.evidenceRequirements",
                "duplicate_evidence_requirement");
        for (int i = 0; i < c.pack.fields().size(); i++) {
            Field f = c.pack.fields().get(i);
            required(c, f.id(), "$.fields[" + i + "].id");
            if (!FIELD_TYPES.contains(f.type())) {
                c.add("unsupported_field_type", "Unsupported field type.", "$.fields[" + i + "].type");
            }
        }
    }

    private static void sources(Context c) {
        duplicates(c, c.pack.sources(), Source::id, "$.sources", "duplicate_source");
        boolean provenance = !"SYNTHETIC".equals(c.pack.manifest().validationLevel());
        for (int i = 0; i < c.pack.sources().size(); i++) {
            Source s = c.pack.sources().get(i);
            String path = "$.sources[" + i + "]";
            required(c, s.id(), path + ".id");
            if (s.validFrom() != null && s.validUntil() != null && s.validUntil().isBefore(s.validFrom())) {
                c.add("invalid_source_validity_interval", "Source validity ends before it starts.", path + ".validUntil");
            }
            if (s.contentHash() != null && !SHA256.matcher(s.contentHash()).matches()) {
                c.add("invalid_source_content_hash", "Content hash must be sha256:<64 hex>.", path + ".contentHash");
            }
            if (provenance) {
                if (blank(s.version())) c.add("missing_source_version", "Source requires version.", path + ".version");
                if (blank(s.sourceLocation())) c.add("missing_source_location", "Source requires sourceLocation.", path + ".sourceLocation");
                if (s.retrievedAt() == null) c.add("missing_source_retrieved_at", "Source requires retrievedAt.", path + ".retrievedAt");
                if (blank(s.contentHash())) c.add("missing_source_content_hash", "Source requires contentHash.", path + ".contentHash");
            }
        }
    }

    private static void rules(Context c) {
        for (int i = 0; i < c.pack.rules().size(); i++) {
            Rule r = c.pack.rules().get(i);
            String path = "$.rules[" + i + "]";
            required(c, r.id(), path + ".id");
            if (r.version() < 1) c.add("invalid_rule_version", "Rule version must be positive.", path + ".version");
            if (r.validUntil() != null && r.validUntil().isBefore(r.validFrom())) {
                c.add("invalid_validity_interval", "Rule validity ends before it starts.", path + ".validUntil");
            }
            if (!c.sources.contains(r.sourceId())) c.add("missing_source", "Rule references an undeclared source.", path + ".sourceId");
            if (r.onMatch() == null) c.add("missing_on_match", "onMatch is required.", path + ".onMatch");
            if (r.onNoMatch() == null) c.add("missing_on_no_match", "onNoMatch is required.", path + ".onNoMatch");
            if (r.onUnknown() != null && r.onUnknown() != AssessmentOutcome.INCOMPLETE && r.onUnknown() != AssessmentOutcome.HUMAN_REVIEW) {
                c.add("unsafe_on_unknown", "onUnknown must be INCOMPLETE or HUMAN_REVIEW.", path + ".onUnknown");
            }
            condition(c, r.condition(), path + ".condition");
        }
        versionsAndOverlaps(c, c.pack.rules(), Rule::id, Rule::version, Rule::validFrom, Rule::validUntil, "$.rules",
                "duplicate_rule_version", "overlapping_rule_validity");
    }

    private static void outputs(Context c) {
        for (int i = 0; i < c.pack.outputs().size(); i++) {
            Output o = c.pack.outputs().get(i);
            String path = "$.outputs[" + i + "]";
            required(c, o.id(), path + ".id");
            if (o.version() < 1) c.add("invalid_output_version", "Output version must be positive.", path + ".version");
            if (o.validUntil() != null && o.validUntil().isBefore(o.validFrom())) {
                c.add("invalid_output_validity_interval", "Output validity ends before it starts.", path + ".validUntil");
            }
            if (!c.sources.contains(o.sourceId())) c.add("missing_output_source", "Output references an undeclared source.", path + ".sourceId");
            if (!OUTPUT_TYPES.contains(o.type())) c.add("unsupported_output_type", "Unsupported output type.", path + ".type");
            if (o.choices().isEmpty()) c.add("empty_output_choices", "Output needs choices.", path + ".choices");
            Set<String> seen = new HashSet<>();
            for (int j = 0; j < o.choices().size(); j++) {
                String choice = o.choices().get(j);
                if (blank(choice)) c.add("blank_output_choice", "Choice must not be blank.", path + ".choices[" + j + "]");
                else if (choice.equalsIgnoreCase("UNKNOWN")) c.add("reserved_output_choice", "UNKNOWN is reserved.", path + ".choices[" + j + "]");
                else if (!seen.add(choice)) c.add("duplicate_output_choice", "Duplicate choice.", path + ".choices[" + j + "]");
            }
            if ("number".equals(o.type())) {
                if (o.condition() != null || o.onMatch() != null || o.onNoMatch() != null) {
                    c.add("invalid_number_output", "Number outputs have no condition or branches.", path);
                }
                if (o.numericExpression() == null) {
                    c.add("missing_numeric_expression", "Number outputs need a numericExpression.", path + ".numericExpression");
                } else {
                    numeric(c, o.numericExpression(), path + ".numericExpression");
                }
                for (int j = 0; j < o.choices().size(); j++) {
                    try {
                        new java.math.BigDecimal(o.choices().get(j));
                    } catch (NumberFormatException e) {
                        c.add("invalid_number_output_choice", "Choices of number outputs are numbers.", path + ".choices[" + j + "]");
                    }
                }
                continue;
            }
            if (o.numericExpression() != null) c.add("invalid_choice_output", "Choice outputs use a condition.", path + ".numericExpression");
            if (o.condition() == null) {
                c.add("missing_output_condition", "Choice outputs need a condition.", path + ".condition");
                continue;
            }
            branch(c, o, o.onMatch(), path + ".onMatch");
            branch(c, o, o.onNoMatch(), path + ".onNoMatch");
            condition(c, o.condition(), path + ".condition");
        }
        versionsAndOverlaps(c, c.pack.outputs(), Output::id, Output::version, Output::validFrom, Output::validUntil, "$.outputs",
                "duplicate_output_version", "overlapping_output_validity");
    }

    private static void branch(Context c, Output o, String value, String path) {
        if (blank(value)) c.add("missing_output_branch_value", "Branch value is required.", path);
        else if (!o.choices().contains(value)) c.add("invalid_output_branch_value", "Branch value must be a declared choice.", path);
    }

    private static void workflows(Context c) {
        for (int i = 0; i < c.pack.workflows().size(); i++) {
            Workflow w = c.pack.workflows().get(i);
            String path = "$.workflows[" + i + "]";
            if (!c.sources.contains(w.sourceId())) c.add("missing_workflow_source", "Workflow references an undeclared source.", path + ".sourceId");
            Set<String> states = w.states().stream().map(KnowledgePack.WorkflowState::id).collect(Collectors.toSet());
            boolean valid = w.version() >= 1 && states.size() == w.states().size() && states.contains(w.initialStateId())
                    && w.transitions().stream().allMatch(t -> states.contains(t.fromStateId()) && states.contains(t.toStateId()));
            if (!valid) c.add("invalid_workflow_definition", "Workflow states or transitions are inconsistent.", path);
        }
    }

    private static void condition(Context c, Condition cond, String path) {
        String kind = cond.kind();
        boolean leaf = LEAF_CONDITIONS.contains(kind);
        if (!leaf && !GROUP_CONDITIONS.contains(kind)) {
            c.add("unknown_condition_kind", "Unknown condition kind.", path + ".kind");
            return;
        }
        if (cond.evidenceRequirementId() != null && !"requires_evidence".equals(kind)) {
            c.add("unexpected_evidence_reference", "Only requires_evidence can reference evidence.", path + ".evidenceRequirementId");
        }
        if (leaf) {
            if (!cond.conditions().isEmpty()) c.add("leaf_has_children", "Leaf conditions cannot have children.", path + ".conditions");
            switch (kind) {
                case "field_equals" -> {
                    if (cond.numericExpression() != null) c.add("unexpected_numeric_expression", "field_equals has no numeric expression.", path);
                    if (cond.expected() == null) c.add("missing_expected_value", "expected is required.", path + ".expected");
                    fieldOfType(c, cond.field(), "truth", kind, path);
                }
                case "number_gte" -> {
                    if (cond.threshold() == null) c.add("missing_threshold", "threshold is required.", path + ".threshold");
                    numericInput(c, cond, kind, path);
                }
                default -> {
                    if (cond.minimum() == null) c.add("missing_minimum", "minimum is required.", path + ".minimum");
                    if (cond.maximum() == null) c.add("missing_maximum", "maximum is required.", path + ".maximum");
                    if (cond.minimum() != null && cond.maximum() != null && cond.minimum().compareTo(cond.maximum()) > 0) {
                        c.add("invalid_numeric_range", "minimum must not exceed maximum.", path);
                    }
                    numericInput(c, cond, kind, path);
                }
            }
            return;
        }
        if (cond.field() != null || cond.expected() != null || cond.threshold() != null || cond.minimum() != null
                || cond.maximum() != null || cond.numericExpression() != null) {
            c.add("requires_evidence".equals(kind) ? "ambiguous_evidence_dependency"
                    : "not".equals(kind) ? "ambiguous_negation" : "leaf_has_children",
                    "Group conditions cannot declare comparisons.", path);
        }
        switch (kind) {
            case "not" -> {
                if (cond.conditions().size() != 1) c.add("invalid_negation", "not must contain exactly one condition.", path + ".conditions");
            }
            case "requires_evidence" -> {
                if (cond.conditions().size() != 1) c.add("invalid_evidence_dependency", "requires_evidence must contain exactly one condition.", path + ".conditions");
                if (cond.evidenceRequirementId() == null || !c.evidence.contains(cond.evidenceRequirementId())) {
                    c.add("missing_evidence_requirement", "Condition references undeclared evidence.", path + ".evidenceRequirementId");
                }
            }
            default -> {
                if (cond.conditions().isEmpty()) c.add("empty_condition_group", "Condition groups need children.", path + ".conditions");
            }
        }
        for (int i = 0; i < cond.conditions().size(); i++) {
            condition(c, cond.conditions().get(i), path + ".conditions[" + i + "]");
        }
    }

    private static void numericInput(Context c, Condition cond, String kind, String path) {
        if (cond.field() != null && cond.numericExpression() != null) {
            c.add("ambiguous_numeric_input", "Use either field or numericExpression.", path);
        } else if (cond.field() == null && cond.numericExpression() == null) {
            c.add("missing_numeric_input", "field or numericExpression is required.", path);
        } else if (cond.field() != null) {
            fieldOfType(c, cond.field(), "number", kind, path);
        } else {
            numeric(c, cond.numericExpression(), path + ".numericExpression");
        }
        if (cond.expected() != null) c.add("unknown_expected_value", "Numeric conditions have no expected truth value.", path + ".expected");
    }

    private static void numeric(Context c, NumericExpression e, String path) {
        if (!NUMERIC_KINDS.contains(e.kind())) {
            c.add("unknown_numeric_expression_kind", "Unknown numeric expression kind.", path + ".kind");
            return;
        }
        if (e.id() != null && !e.id().matches("^[a-z][a-z0-9_]{0,63}$")) {
            c.add("invalid_numeric_expression_id", "id must be a lower-case technical identifier.", path + ".id");
        }
        boolean scaling = e.factor() != null || e.divisor() != null || e.decimals() != null;
        if (scaling && !"scale".equals(e.kind())) shape(c, path);
        switch (e.kind()) {
            case "scale" -> {
                if (e.field() != null || !e.operands().isEmpty() || !e.bands().isEmpty()) shape(c, path);
                if (e.input() == null) c.add("missing_numeric_expression_input", "input is required.", path + ".input");
                else numeric(c, e.input(), path + ".input");
                if ((e.factor() == null) == (e.divisor() == null)) {
                    c.add("invalid_scale", "scale needs exactly one of factor or divisor.", path);
                } else if (e.divisor() != null && e.divisor().signum() <= 0) {
                    c.add("invalid_scale", "divisor must be positive.", path + ".divisor");
                } else if (e.divisor() != null && e.decimals() == null) {
                    c.add("invalid_scale", "division needs decimals for deterministic rounding.", path + ".decimals");
                }
                if (e.decimals() != null && (e.decimals() < 0 || e.decimals() > 10)) {
                    c.add("invalid_scale", "decimals must be between 0 and 10.", path + ".decimals");
                }
            }
            case "field" -> {
                if (e.input() != null || !e.operands().isEmpty() || !e.bands().isEmpty()) shape(c, path);
                if (e.field() == null) c.add("missing_numeric_expression_field", "field is required.", path + ".field");
                else if (!c.fields.containsKey(e.field())) c.add("missing_field", "Field is not declared.", path + ".field");
                else if (!"number".equals(c.fields.get(e.field()).type())) {
                    c.add("numeric_expression_field_type_mismatch", "Numeric expressions need number fields.", path + ".field");
                }
            }
            case "range_lookup" -> {
                if (e.field() != null || !e.operands().isEmpty()) shape(c, path);
                if (e.input() == null) c.add("missing_numeric_expression_input", "input is required.", path + ".input");
                else numeric(c, e.input(), path + ".input");
                bands(c, e.bands(), path + ".bands");
            }
            default -> {
                if (e.field() != null || e.input() != null || !e.bands().isEmpty()) shape(c, path);
                if (e.operands().isEmpty()) c.add("empty_numeric_operands", "operands are required.", path + ".operands");
                for (int i = 0; i < e.operands().size(); i++) numeric(c, e.operands().get(i), path + ".operands[" + i + "]");
            }
        }
    }

    private static void bands(Context c, List<RangeBand> bands, String path) {
        if (bands.isEmpty()) {
            c.add("empty_numeric_bands", "Range lookups need bands.", path);
            return;
        }
        RangeBand previous = null;
        for (int i = 0; i < bands.size(); i++) {
            RangeBand b = bands.get(i);
            String p = path + "[" + i + "]";
            if (b.minimum() == null || b.maximum() == null || b.value() == null) {
                c.add("incomplete_numeric_band", "Bands need minimum, maximum and value.", p);
                continue;
            }
            if (b.minimum().compareTo(b.maximum()) > 0) c.add("invalid_numeric_band", "Band minimum exceeds maximum.", p);
            if (previous != null && b.minimum().compareTo(previous.maximum()) <= 0) {
                c.add("invalid_numeric_band_order", "Bands must be ordered and must not overlap.", p);
            }
            previous = b;
        }
    }

    private static void shape(Context c, String path) {
        c.add("invalid_numeric_expression_shape", "Numeric expression declares properties of another kind.", path);
    }

    private static void fieldOfType(Context c, String fieldId, String type, String kind, String path) {
        if (fieldId == null) {
            c.add("missing_field", "field is required.", path + ".field");
        } else if (!c.fields.containsKey(fieldId)) {
            c.add("missing_field", "Field is not declared.", path + ".field");
        } else if (!type.equals(c.fields.get(fieldId).type())) {
            c.add("condition_field_type_mismatch", "Condition '" + kind + "' requires field type '" + type + "'.", path + ".field");
        }
    }

    private static <T> void versionsAndOverlaps(Context c, List<T> items, Function<T, String> id, Function<T, Integer> version,
            Function<T, LocalDate> from, Function<T, LocalDate> until, String path, String duplicateCode, String overlapCode) {
        Map<String, List<T>> groups = items.stream().collect(Collectors.groupingBy(id));
        for (var group : groups.entrySet()) {
            Set<Integer> versions = new HashSet<>();
            for (T item : group.getValue()) {
                if (!versions.add(version.apply(item))) {
                    c.add(duplicateCode, "Duplicate version of '" + group.getKey() + "'.", path);
                }
            }
            List<T> ordered = group.getValue().stream().filter(i -> from.apply(i) != null)
                    .sorted(Comparator.comparing(from)).toList();
            for (int i = 1; i < ordered.size(); i++) {
                LocalDate previousUntil = until.apply(ordered.get(i - 1));
                if (previousUntil == null || !previousUntil.isBefore(from.apply(ordered.get(i)))) {
                    c.add(overlapCode, "Validity intervals of '" + group.getKey() + "' overlap.", path);
                }
            }
        }
    }

    private static <T> void duplicates(Context c, List<T> items, Function<T, String> id, String path, String code) {
        Set<String> seen = new HashSet<>();
        for (T item : items) {
            if (!seen.add(id.apply(item))) {
                c.add(code, "Duplicate identifier '" + id.apply(item) + "'.", path);
            }
        }
    }

    private static void required(Context c, String value, String path) {
        if (blank(value)) {
            c.add("required_value_missing", "Value is required.", path);
        }
    }

    private static boolean blank(String value) {
        return value == null || value.isBlank();
    }
}
