package de.normacase.assessment;

import de.normacase.identity.Actor;
import de.normacase.knowledge.AssessmentOutcome;
import java.time.Instant;
import java.time.LocalDate;
import java.util.List;
import java.util.UUID;

/**
 * Anwendungsereignis nach jeder Bewertung. Enthält ausschließlich Nachweisdaten: Hashes, Versionen, Regel-IDs,
 * Teilergebnisse. Keine Werte, keine Freitexte, keine Personendaten des Falls.
 */
public record AssessmentCompleted(UUID evidenceId, Instant recordedAt, ExternalReference externalReference,
        String inputSha256, String platformVersion, String knowledgeRelease, String knowledgeSha256,
        LocalDate assessmentDate, String ruleId, Integer ruleVersion, AssessmentOutcome outcome,
        List<Criterion> criteria, Actor actor) {

    public AssessmentCompleted {
        criteria = List.copyOf(criteria);
    }

    /** Ein Knoten des Entscheidungspfads ohne den konkreten Eingabewert. */
    public record Criterion(String path, String kind, String field, String evidenceRequirementId, String result,
            String evidenceStatus) {
    }
}
