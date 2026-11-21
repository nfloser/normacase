package de.normacase.knowledge;

import java.util.List;
import java.util.Optional;

/** Zugriff auf die geladenen Knowledge Releases. */
public interface KnowledgeCatalog {

    Optional<KnowledgeRelease> release(String releaseId);

    List<KnowledgeRelease> releases();
}
