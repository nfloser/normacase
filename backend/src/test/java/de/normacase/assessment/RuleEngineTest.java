package de.normacase.assessment;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import de.normacase.knowledge.AssessmentOutcome;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePackReader;
import de.normacase.knowledge.TruthValue;
import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.Map;
import org.junit.jupiter.api.Test;

class RuleEngineTest {

    static final KnowledgePack PACK = new KnowledgePackReader().read("""
            {"manifest": {"formatVersion": 1, "packId": "p", "releaseId": "r-1", "lifecycleStatus": "ACTIVE",
              "validationLevel": "SYNTHETIC", "entryRuleId": "R"},
             "fields": [{"id": "a", "type": "truth", "required": true}, {"id": "n", "type": "number", "required": false}],
             "evidenceRequirements": [{"id": "beleg"}],
             "sources": [{"id": "S", "authority": "Test", "title": "T", "documentType": "SYNTHETIC", "status": "ACTIVE"}],
             "rules": [{"id": "R", "version": 1, "validFrom": "2026-01-01", "validUntil": "2026-12-31", "sourceId": "S",
               "condition": {"kind": "all", "conditions": [
                 {"kind": "field_equals", "field": "a", "expected": "YES"},
                 {"kind": "requires_evidence", "evidenceRequirementId": "beleg", "conditions": [
                   {"kind": "number_gte", "field": "n", "threshold": 2.50}]}]},
               "onMatch": "SUPPORTED", "onNoMatch": "NOT_SUPPORTED", "onUnknown": "HUMAN_REVIEW"}]}""");

    final RuleEngine engine = new RuleEngine();

    static CaseInput input(String date, Map<String, CaseValue> facts, Map<String, EvidenceStatus> evidence) {
        return new CaseInput(LocalDate.parse(date), facts, evidence);
    }

    @Test
    void supportedWhenAllCriteriaAndEvidenceArePresent() {
        var result = engine.evaluate(PACK, input("2026-05-01",
                Map.of("a", CaseValue.truth(TruthValue.YES), "n", CaseValue.number(new BigDecimal("2.5"))),
                Map.of("beleg", EvidenceStatus.PRESENT)));
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.SUPPORTED);
        assertThat(result.ruleTrace().source().id()).isEqualTo("S");
    }

    @Test
    void unknownIsNeverCoercedToNo() {
        var result = engine.evaluate(PACK, input("2026-05-01",
                Map.of("a", CaseValue.truth(TruthValue.YES), "n", CaseValue.UNKNOWN), Map.of("beleg", EvidenceStatus.PRESENT)));
        assertThat(result.ruleTrace().conditionResult()).isEqualTo(ConditionResult.UNKNOWN);
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
    }

    @Test
    void missingRequiredFieldMakesTheResultIncomplete() {
        var result = engine.evaluate(PACK, input("2026-05-01", Map.of(), Map.of()));
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.INCOMPLETE);
        assertThat(result.missingRequiredFields()).containsExactly("a");
    }

    @Test
    void missingEvidenceKeepsTheCriterionOpen() {
        var result = engine.evaluate(PACK, input("2026-05-01",
                Map.of("a", CaseValue.truth(TruthValue.YES), "n", CaseValue.number(BigDecimal.TEN)), Map.of()));
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
    }

    @Test
    void conflictingEvidenceAlwaysGoesToHumanReview() {
        var result = engine.evaluate(PACK, input("2026-05-01",
                Map.of("a", CaseValue.truth(TruthValue.NO), "n", CaseValue.number(BigDecimal.TEN)),
                Map.of("beleg", EvidenceStatus.CONFLICTING)));
        assertThat(result.ruleTrace().conditionResult()).isEqualTo(ConditionResult.NOT_MATCHED);
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
    }

    @Test
    void ruleOutsideItsValidityLeadsToHumanReviewWithoutTrace() {
        var result = engine.evaluate(PACK, input("2027-01-01", Map.of("a", CaseValue.truth(TruthValue.YES)), Map.of()));
        assertThat(result.outcome()).isEqualTo(AssessmentOutcome.HUMAN_REVIEW);
        assertThat(result.ruleTrace()).isNull();
    }

    @Test
    void sameInputGivesTheSameDocument() {
        CaseInput in = input("2026-05-01", Map.of("a", CaseValue.truth(TruthValue.YES), "n", CaseValue.number(new BigDecimal("3.10"))),
                Map.of("beleg", EvidenceStatus.PRESENT));
        String first = AssessmentJson.write(engine.evaluate(PACK, in), "test");
        for (int i = 0; i < 20; i++) {
            assertThat(AssessmentJson.write(engine.evaluate(PACK, in), "test")).isEqualTo(first);
        }
        assertThat(first).contains("\"number\":3.10");
    }

    @Test
    void rejectsUndeclaredFieldsAndWrongTypes() {
        assertThatThrownBy(() -> engine.evaluate(PACK, input("2026-05-01", Map.of("x", CaseValue.UNKNOWN), Map.of())))
                .isInstanceOfSatisfying(InvalidCaseInputException.class, e -> assertThat(e.fields()).containsExactly("x"));
        assertThatThrownBy(() -> engine.evaluate(PACK, input("2026-05-01", Map.of("a", CaseValue.number(BigDecimal.ONE)), Map.of())))
                .isInstanceOfSatisfying(InvalidCaseInputException.class, e -> assertThat(e.fields()).containsExactly("a"));
    }

    @Test
    void caseInputRejectsAmbiguousValues() {
        assertThatThrownBy(() -> AssessmentJson.readCase("""
                {"formatVersion": 1, "assessmentDate": "2026-05-01",
                 "facts": {"a": {"kind": "TRUTH", "truth": "UNKNOWN"}}}"""))
                .isInstanceOf(de.normacase.common.InvalidDocumentException.class);
        assertThatThrownBy(() -> AssessmentJson.readCase("""
                {"formatVersion": 1, "assessmentDate": "2026-05-01",
                 "facts": {"a": {"kind": "UNKNOWN", "truth": "YES"}}}"""))
                .isInstanceOf(de.normacase.common.InvalidDocumentException.class);
    }
}
