package de.normacase.assessment;

import de.normacase.assessment.AssessmentResult.ConditionTrace;
import de.normacase.assessment.AssessmentResult.NumericTrace;
import de.normacase.assessment.AssessmentResult.OutputTrace;
import de.normacase.assessment.AssessmentResult.RuleTrace;
import de.normacase.assessment.AssessmentResult.SourceTrace;
import de.normacase.knowledge.AssessmentOutcome;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePack.Condition;
import de.normacase.knowledge.KnowledgePack.Field;
import de.normacase.knowledge.KnowledgePack.NumericExpression;
import de.normacase.knowledge.KnowledgePack.Output;
import de.normacase.knowledge.KnowledgePack.RangeBand;
import de.normacase.knowledge.KnowledgePack.Rule;
import de.normacase.knowledge.KnowledgePackValidator;
import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.stream.Collectors;

/**
 * Deterministische Regelauswertung als reine Funktion {@code (Regelwerk, Fall, Stichtag) → Ergebnis}.
 * Keine Systemuhr, kein Netzwerk, kein Zufall, keine Fremdlogik. Verhalten entspricht dem Referenzstand.
 */
public final class RuleEngine {

    private final KnowledgePackValidator validator = new KnowledgePackValidator();

    public AssessmentResult evaluate(KnowledgePack pack, CaseInput input) {
        validator.validateOrThrow(pack);
        Map<String, Field> fields = pack.fields().stream().collect(Collectors.toMap(Field::id, f -> f, (a, b) -> a));
        validateFacts(fields, input.facts());
        validateEvidence(pack, input.evidence());
        LocalDate date = input.assessmentDate();
        Map<String, CaseValue> facts = input.facts();
        Map<String, EvidenceStatus> evidence = input.evidence();

        List<String> missing = pack.fields().stream()
                .filter(Field::required)
                .filter(f -> facts.getOrDefault(f.id(), CaseValue.UNKNOWN).isUnknown())
                .map(Field::id)
                .sorted()
                .toList();

        List<OutputTrace> outputs = outputs(pack, facts, evidence, date);

        List<Rule> candidates = pack.rules().stream()
                .filter(r -> r.id().equals(pack.manifest().entryRuleId()))
                .filter(r -> r.validOn(date))
                .toList();
        if (candidates.size() > 1) {
            throw new IllegalStateException("Multiple active versions of entry rule " + pack.manifest().entryRuleId());
        }
        if (candidates.isEmpty()) {
            return new AssessmentResult(pack.manifest().releaseId(), date, AssessmentOutcome.HUMAN_REVIEW, missing, null, outputs);
        }
        Rule rule = candidates.getFirst();
        ConditionTrace condition = condition(rule.condition(), facts, evidence);
        AssessmentOutcome ruleOutcome = switch (condition.result()) {
            case MATCHED -> rule.onMatch();
            case NOT_MATCHED -> rule.onNoMatch();
            case UNKNOWN -> rule.onUnknown() != null ? rule.onUnknown() : AssessmentOutcome.INCOMPLETE;
        };
        AssessmentOutcome outcome = !missing.isEmpty() ? AssessmentOutcome.INCOMPLETE
                : hasConflict(condition) ? AssessmentOutcome.HUMAN_REVIEW
                : ruleOutcome;
        RuleTrace trace = new RuleTrace(rule.id(), rule.version(), rule.sourceId(), condition.result(), outcome, condition,
                source(pack, rule.sourceId()));
        return new AssessmentResult(pack.manifest().releaseId(), date, outcome, missing, trace, outputs);
    }

    private static List<OutputTrace> outputs(KnowledgePack pack, Map<String, CaseValue> facts,
            Map<String, EvidenceStatus> evidence, LocalDate date) {
        Map<String, List<Output>> groups = new TreeMap<>(pack.outputs().stream().collect(Collectors.groupingBy(Output::id)));
        List<OutputTrace> traces = new ArrayList<>();
        for (var group : groups.entrySet()) {
            List<Output> active = group.getValue().stream().filter(o -> o.validOn(date)).toList();
            if (active.isEmpty()) {
                continue;
            }
            if (active.size() > 1) {
                throw new IllegalStateException("Multiple active versions of domain output " + group.getKey());
            }
            Output output = active.getFirst();
            ConditionTrace condition;
            String value;
            if ("number".equals(output.type())) {
                condition = numberOutput(output, facts);
                value = condition.result() == ConditionResult.MATCHED ? ((CaseValue.Number) condition.actual()).value().toPlainString() : null;
            } else {
                condition = condition(output.condition(), facts, evidence);
                value = switch (condition.result()) {
                    case MATCHED -> output.onMatch();
                    case NOT_MATCHED -> output.onNoMatch();
                    case UNKNOWN -> null;
                };
            }
            traces.add(new OutputTrace(output.id(), output.version(), value, condition.result(), condition,
                    source(pack, output.sourceId())));
        }
        return traces;
    }

    private static SourceTrace source(KnowledgePack pack, String id) {
        KnowledgePack.Source s = pack.sources().stream().filter(x -> x.id().equals(id)).findFirst().orElseThrow();
        return new SourceTrace(s.id(), s.version(), s.sourceLocation(), s.authority(), s.title(), s.documentType(),
                s.status(), s.publicationDate(), s.validFrom(), s.validUntil(), s.retrievedAt(), s.contentHash());
    }

    private static ConditionTrace condition(Condition c, Map<String, CaseValue> facts, Map<String, EvidenceStatus> evidence) {
        return switch (c.kind()) {
            case "field_equals" -> fieldEquals(c, facts);
            case "number_gte" -> numberGte(c, facts);
            case "number_in_range" -> numberInRange(c, facts);
            case "all" -> group(c, facts, evidence, true);
            case "any" -> group(c, facts, evidence, false);
            case "not" -> not(c, facts, evidence);
            case "requires_evidence" -> requiresEvidence(c, facts, evidence);
            default -> throw new IllegalStateException("Validation should reject condition kind " + c.kind());
        };
    }

    private static ConditionTrace fieldEquals(Condition c, Map<String, CaseValue> facts) {
        CaseValue actual = facts.getOrDefault(c.field(), CaseValue.UNKNOWN);
        ConditionResult result = actual.isUnknown() ? ConditionResult.UNKNOWN
                : actual instanceof CaseValue.Truth t && t.value() == c.expected() ? ConditionResult.MATCHED
                : ConditionResult.NOT_MATCHED;
        return new ConditionTrace(c.kind(), result, c.field(), CaseValue.truth(c.expected()), actual, null, null,
                List.of(), null, null, null);
    }

    /** Zahlenausgabe: gültig nur mit bekanntem Wert, der unter den deklarierten Werten steht (fail-closed). */
    private static ConditionTrace numberOutput(Output output, Map<String, CaseValue> facts) {
        NumericTrace expression = numeric(output.numericExpression(), facts);
        boolean declared = expression.value() instanceof CaseValue.Number n
                && output.choices().stream().anyMatch(choice -> new BigDecimal(choice).compareTo(n.value()) == 0);
        CaseValue actual = declared
                ? CaseValue.number(new BigDecimal(output.choices().stream()
                        .filter(choice -> new BigDecimal(choice).compareTo(((CaseValue.Number) expression.value()).value()) == 0)
                        .findFirst().orElseThrow()))
                : expression.value();
        return new ConditionTrace("number_output", declared ? ConditionResult.MATCHED : ConditionResult.UNKNOWN, null, null,
                actual, null, null, List.of(), null, null, expression);
    }

    private static ConditionTrace numberGte(Condition c, Map<String, CaseValue> facts) {
        NumericTrace expression = c.numericExpression() == null ? null : numeric(c.numericExpression(), facts);
        CaseValue actual = expression == null ? facts.getOrDefault(c.field(), CaseValue.UNKNOWN) : expression.value();
        ConditionResult result = actual.isUnknown() ? ConditionResult.UNKNOWN
                : actual instanceof CaseValue.Number n && n.value().compareTo(c.threshold()) >= 0 ? ConditionResult.MATCHED
                : ConditionResult.NOT_MATCHED;
        return new ConditionTrace(c.kind(), result, c.field(), CaseValue.number(c.threshold()), actual, null, null,
                List.of(), null, null, expression);
    }

    private static ConditionTrace numberInRange(Condition c, Map<String, CaseValue> facts) {
        NumericTrace expression = c.numericExpression() == null ? null : numeric(c.numericExpression(), facts);
        CaseValue actual = expression == null ? facts.getOrDefault(c.field(), CaseValue.UNKNOWN) : expression.value();
        ConditionResult result = actual.isUnknown() ? ConditionResult.UNKNOWN
                : actual instanceof CaseValue.Number n && n.value().compareTo(c.minimum()) >= 0
                        && n.value().compareTo(c.maximum()) <= 0 ? ConditionResult.MATCHED
                : ConditionResult.NOT_MATCHED;
        return new ConditionTrace(c.kind(), result, c.field(), null, actual, c.minimum(), c.maximum(), List.of(), null,
                null, expression);
    }

    private static ConditionTrace group(Condition c, Map<String, CaseValue> facts, Map<String, EvidenceStatus> evidence,
            boolean all) {
        List<ConditionTrace> children = c.conditions().stream().map(x -> condition(x, facts, evidence)).toList();
        Set<ConditionResult> results = children.stream().map(ConditionTrace::result).collect(Collectors.toSet());
        ConditionResult decisive = all ? ConditionResult.NOT_MATCHED : ConditionResult.MATCHED;
        ConditionResult result = results.contains(decisive) ? decisive
                : results.contains(ConditionResult.UNKNOWN) ? ConditionResult.UNKNOWN
                : all ? ConditionResult.MATCHED : ConditionResult.NOT_MATCHED;
        return groupTrace(c.kind(), result, children);
    }

    private static ConditionTrace not(Condition c, Map<String, CaseValue> facts, Map<String, EvidenceStatus> evidence) {
        ConditionTrace child = condition(c.conditions().getFirst(), facts, evidence);
        ConditionResult result = switch (child.result()) {
            case MATCHED -> ConditionResult.NOT_MATCHED;
            case NOT_MATCHED -> ConditionResult.MATCHED;
            case UNKNOWN -> ConditionResult.UNKNOWN;
        };
        return groupTrace(c.kind(), result, List.of(child));
    }

    private static ConditionTrace requiresEvidence(Condition c, Map<String, CaseValue> facts,
            Map<String, EvidenceStatus> evidence) {
        EvidenceStatus status = evidence.getOrDefault(c.evidenceRequirementId(), EvidenceStatus.MISSING);
        ConditionTrace child = condition(c.conditions().getFirst(), facts, evidence);
        ConditionResult result = status == EvidenceStatus.PRESENT ? child.result() : ConditionResult.UNKNOWN;
        return new ConditionTrace(c.kind(), result, null, null, null, null, null, List.of(child),
                c.evidenceRequirementId(), status, null);
    }

    private static ConditionTrace groupTrace(String kind, ConditionResult result, List<ConditionTrace> children) {
        return new ConditionTrace(kind, result, null, null, null, null, null, children, null, null, null);
    }

    private static NumericTrace numeric(NumericExpression e, Map<String, CaseValue> facts) {
        NumericTrace trace = switch (e.kind()) {
            case "field" -> new NumericTrace(e.kind(), facts.getOrDefault(e.field(), CaseValue.UNKNOWN), e.field(),
                    null, null, null, List.of());
            case "range_lookup" -> rangeLookup(e, facts);
            case "sum", "max" -> aggregate(e, facts);
            case "scale" -> scale(e, facts);
            default -> throw new IllegalStateException("Validation should reject numeric kind " + e.kind());
        };
        return trace.withId(e.id());
    }

    /** Multipliziert oder teilt exakt (BigDecimal); bei {@code decimals} kaufmännisch gerundet. */
    private static NumericTrace scale(NumericExpression e, Map<String, CaseValue> facts) {
        NumericTrace input = numeric(e.input(), facts);
        CaseValue value = CaseValue.UNKNOWN;
        if (input.value() instanceof CaseValue.Number n) {
            BigDecimal result = e.factor() != null
                    ? n.value().multiply(e.factor())
                    : n.value().divide(e.divisor(), e.decimals(), java.math.RoundingMode.HALF_UP);
            if (e.decimals() != null) result = result.setScale(e.decimals(), java.math.RoundingMode.HALF_UP);
            result = result.stripTrailingZeros();
            value = CaseValue.number(result.scale() < 0 ? result.setScale(0) : result);
        }
        return new NumericTrace(e.kind(), value, null, null, null, null, List.of(input), null, e.factor(), e.divisor());
    }

    private static NumericTrace rangeLookup(NumericExpression e, Map<String, CaseValue> facts) {
        NumericTrace input = numeric(e.input(), facts);
        if (!(input.value() instanceof CaseValue.Number n)) {
            return new NumericTrace(e.kind(), CaseValue.UNKNOWN, null, null, null, null, List.of(input));
        }
        for (RangeBand band : e.bands()) {
            if (band.minimum().compareTo(n.value()) <= 0 && n.value().compareTo(band.maximum()) <= 0) {
                return new NumericTrace(e.kind(), CaseValue.number(band.value()), null, band.minimum(), band.maximum(),
                        band.value(), List.of(input));
            }
        }
        return new NumericTrace(e.kind(), CaseValue.UNKNOWN, null, null, null, null, List.of(input));
    }

    private static NumericTrace aggregate(NumericExpression e, Map<String, CaseValue> facts) {
        List<NumericTrace> children = e.operands().stream().map(o -> numeric(o, facts)).toList();
        CaseValue value;
        if (children.stream().anyMatch(c -> c.value().isUnknown())) {
            value = CaseValue.UNKNOWN;
        } else {
            List<BigDecimal> numbers = children.stream().map(c -> ((CaseValue.Number) c.value()).value()).toList();
            value = CaseValue.number("sum".equals(e.kind())
                    ? numbers.stream().reduce(BigDecimal.ZERO, BigDecimal::add)
                    : max(numbers));
        }
        return new NumericTrace(e.kind(), value, null, null, null, null, children);
    }

    /** Wie {@code Enumerable.Max} im Referenzstand: bei gleichem Wert gewinnt der erste. */
    private static BigDecimal max(List<BigDecimal> numbers) {
        BigDecimal best = numbers.getFirst();
        for (BigDecimal n : numbers) {
            if (n.compareTo(best) > 0) {
                best = n;
            }
        }
        return best;
    }

    private static boolean hasConflict(ConditionTrace trace) {
        return trace.evidenceStatus() == EvidenceStatus.CONFLICTING
                || trace.children().stream().anyMatch(RuleEngine::hasConflict);
    }

    private static void validateFacts(Map<String, Field> fields, Map<String, CaseValue> facts) {
        List<String> undeclared = facts.keySet().stream().filter(k -> !fields.containsKey(k)).sorted().toList();
        if (!undeclared.isEmpty()) {
            throw new InvalidCaseInputException("Case contains fields not declared by the Knowledge Pack", undeclared);
        }
        List<String> mismatched = facts.entrySet().stream()
                .filter(e -> !e.getValue().isUnknown())
                .filter(e -> !matches(e.getValue(), fields.get(e.getKey()).type()))
                .map(Map.Entry::getKey)
                .sorted()
                .toList();
        if (!mismatched.isEmpty()) {
            throw new InvalidCaseInputException("Case values do not match their declared field types", mismatched);
        }
    }

    private static boolean matches(CaseValue value, String type) {
        return switch (type) {
            case "truth" -> value instanceof CaseValue.Truth;
            case "number" -> value instanceof CaseValue.Number;
            default -> false;
        };
    }

    private static void validateEvidence(KnowledgePack pack, Map<String, EvidenceStatus> evidence) {
        Set<String> declared = pack.evidenceRequirements().stream().map(KnowledgePack.EvidenceRequirement::id)
                .collect(Collectors.toSet());
        List<String> undeclared = evidence.keySet().stream().filter(k -> !declared.contains(k)).sorted().toList();
        if (!undeclared.isEmpty()) {
            throw new InvalidCaseInputException("Evidence contains undeclared requirements", undeclared);
        }
    }
}
