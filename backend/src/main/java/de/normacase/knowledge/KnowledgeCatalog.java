package de.normacase.knowledge;

import java.util.List;
import java.util.Optional;

/** Zugriff auf die geladenen Knowledge Releases. */
public interface KnowledgeCatalog {

    Optional<KnowledgeRelease> release(String releaseId);

    List<KnowledgeRelease> releases();

    /**
     * Synthetisches Beispiel eines Releases als Falleingabe (JSON), wie in {@code presentation.de-DE.json} unter
     * {@code examples} aufgeführt. Beispiele sind Vorführ- und Prüfmaterial, keine Fallakten.
     */
    default Optional<String> example(String releaseId, String exampleId) {
        return Optional.empty();
    }

    /** Beispiele eines Releases mit Bezeichnung und dokumentiertem Ergebnis (aus {@code examples/expected-outcomes.json}). */
    default List<Example> examples(String releaseId) {
        return List.of();
    }

    record Example(String id, String label, String expectedOutcome) {
    }
}
