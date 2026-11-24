package de.normacase.assessment;

import de.normacase.knowledge.AssessmentOutcome;
import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.List;

/** Ergebnis mit vollständigem Entscheidungspfad: Eingabe → Kriterium → Regel → Quelle → Ergebnis. */
public record AssessmentResult(String knowledgeRelease, LocalDate assessmentDate, AssessmentOutcome outcome,
        List<String> missingRequiredFields, RuleTrace ruleTrace, List<OutputTrace> domainOutputs) {

    public AssessmentResult {
        missingRequiredFields = List.copyOf(missingRequiredFields);
        domainOutputs = List.copyOf(domainOutputs);
    }

    public record RuleTrace(String ruleId, int ruleVersion, String sourceId, ConditionResult conditionResult,
            AssessmentOutcome outcome, ConditionTrace condition, SourceTrace source) {
    }

    public record ConditionTrace(String kind, ConditionResult result, String field, CaseValue expected, CaseValue actual,
            BigDecimal minimum, BigDecimal maximum, List<ConditionTrace> children, String evidenceRequirementId,
            EvidenceStatus evidenceStatus, NumericTrace numericExpression) {

        public ConditionTrace {
            children = List.copyOf(children);
        }
    }

    public record NumericTrace(String kind, CaseValue value, String field, BigDecimal selectedMinimum,
            BigDecimal selectedMaximum, BigDecimal selectedValue, List<NumericTrace> children) {

        public NumericTrace {
            children = List.copyOf(children);
        }
    }

    /** {@code value == null} bedeutet: Ergebnis unbekannt. */
    public record OutputTrace(String outputId, int outputVersion, String value, ConditionResult conditionResult,
            ConditionTrace condition, SourceTrace source) {
    }

    public record SourceTrace(String id, String version, String sourceLocation, String authority, String title,
            String documentType, String status, LocalDate publicationDate, LocalDate validFrom, LocalDate validUntil,
            LocalDate retrievedAt, String contentHash) {
    }
}
