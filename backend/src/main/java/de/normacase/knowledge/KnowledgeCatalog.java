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
}
