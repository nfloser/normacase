package de.normacase.knowledge;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.List;

/**
 * Ein versioniertes, quellgebundenes Regelwerk. Fachliches Wissen lebt ausschließlich hier,
 * nicht im Plattformcode ({@code PLATFORM != DOMAIN KNOWLEDGE}).
 */
public record KnowledgePack(
        Manifest manifest,
        List<Field> fields,
        List<EvidenceRequirement> evidenceRequirements,
        List<Source> sources,
        List<Rule> rules,
        List<Output> outputs,
        List<Workflow> workflows) {

    public KnowledgePack {
        fields = List.copyOf(fields);
        evidenceRequirements = List.copyOf(evidenceRequirements);
        sources = List.copyOf(sources);
        rules = List.copyOf(rules);
        outputs = List.copyOf(outputs);
        workflows = List.copyOf(workflows);
    }

    public record Manifest(int formatVersion, String packId, String releaseId, String lifecycleStatus,
            String validationLevel, String entryRuleId) {
    }

    public record Field(String id, String type, boolean required) {
    }

    public record EvidenceRequirement(String id) {
    }

    public record Source(String id, String authority, String title, String documentType, LocalDate publicationDate,
            LocalDate validFrom, LocalDate validUntil, String version, String sourceLocation, LocalDate retrievedAt,
            String contentHash, String status) {
    }

    public record Rule(String id, int version, LocalDate validFrom, LocalDate validUntil, String sourceId,
            Condition condition, AssessmentOutcome onMatch, AssessmentOutcome onNoMatch, AssessmentOutcome onUnknown) {

        public boolean validOn(LocalDate date) {
            return !validFrom.isAfter(date) && (validUntil == null || !date.isAfter(validUntil));
        }
    }

    /**
     * Teilbewertung. {@code choice}: Bedingung → {@code onMatch}/{@code onNoMatch}. {@code number}: Wert des
     * Zahlenausdrucks, nur gültig, wenn er unter {@code choices} steht (sonst UNKNOWN).
     */
    public record Output(String id, int version, LocalDate validFrom, LocalDate validUntil, String sourceId,
            String type, List<String> choices, Condition condition, String onMatch, String onNoMatch,
            NumericExpression numericExpression) {

        public Output {
            choices = List.copyOf(choices);
        }

        public Output(String id, int version, LocalDate validFrom, LocalDate validUntil, String sourceId,
                String type, List<String> choices, Condition condition, String onMatch, String onNoMatch) {
            this(id, version, validFrom, validUntil, sourceId, type, choices, condition, onMatch, onNoMatch, null);
        }

        public boolean validOn(LocalDate date) {
            return !validFrom.isAfter(date) && (validUntil == null || !date.isAfter(validUntil));
        }
    }

    /** Bedingungsknoten; welche Felder gesetzt sein dürfen, prüft {@link KnowledgePackValidator}. */
    public record Condition(String kind, String field, String evidenceRequirementId, TruthValue expected,
            BigDecimal threshold, BigDecimal minimum, BigDecimal maximum, NumericExpression numericExpression,
            List<Condition> conditions) {

        public Condition {
            conditions = List.copyOf(conditions);
        }
    }

    /**
     * Zahlenausdruck. {@code id} benennt einen Zwischenwert (z. B. ein Modul), damit die Begründung ihn beschriften
     * kann; {@code scale} multipliziert mit {@code factor} oder teilt durch {@code divisor}, optional gerundet auf
     * {@code decimals} Nachkommastellen (kaufmännisch).
     */
    public record NumericExpression(String kind, String field, NumericExpression input, List<NumericExpression> operands,
            List<RangeBand> bands, String id, BigDecimal factor, BigDecimal divisor, Integer decimals) {

        public NumericExpression {
            operands = List.copyOf(operands);
            bands = List.copyOf(bands);
        }

        public NumericExpression(String kind, String field, NumericExpression input, List<NumericExpression> operands,
                List<RangeBand> bands) {
            this(kind, field, input, operands, bands, null, null, null, null);
        }
    }

    public record RangeBand(BigDecimal minimum, BigDecimal maximum, BigDecimal value) {
    }

    public record Workflow(String id, int version, String sourceId, String initialStateId, List<WorkflowState> states,
            List<WorkflowTransition> transitions) {

        public Workflow {
            states = List.copyOf(states);
            transitions = List.copyOf(transitions);
        }
    }

    public record WorkflowState(String id, boolean terminal) {
    }

    public record WorkflowTransition(String id, String fromStateId, String toStateId) {
    }
}
