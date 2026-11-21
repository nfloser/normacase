package de.normacase.documents;

import java.util.List;
import java.util.Optional;
import java.util.UUID;

/**
 * Dokumente eines Vorgangs für die Dauer der Bearbeitung. NormaCase interpretiert Dokumente nicht;
 * sie dienen der Gutachterin als Beleg in der Fallakte und werden mit dem Vorgang gelöscht.
 */
public interface DocumentStore {

    void store(UUID processId, List<IncomingDocument> documents);

    List<DocumentInfo> list(UUID processId);

    Optional<DocumentContent> content(UUID processId, String documentId);

    Optional<byte[]> page(UUID processId, String documentId, int page);

    void deleteAll(UUID processId);

    /** Eingelieferter Inhalt; {@code pages} sind optionale PNG-Seitenvorschauen für PDFs. */
    record IncomingDocument(String id, String title, String mediaType, byte[] content, List<byte[]> pages) {
        public IncomingDocument {
            pages = List.copyOf(pages);
        }
    }

    record DocumentInfo(String id, String title, String mediaType, String sha256, int sizeBytes, int pageCount) {
    }

    record DocumentContent(DocumentInfo info, byte[] content) {
    }
}
