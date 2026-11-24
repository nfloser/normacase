package de.normacase.documents;

import java.time.LocalDate;
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

    /**
     * Eingelieferter Inhalt; {@code pages} sind optionale PNG-Seitenvorschauen für PDFs. Für TIFF-Scans erzeugt
     * NormaCase die Vorschauen selbst. {@code category}, {@code issuedOn} und {@code references} ordnen die Fallakte
     * und belegen, auf welcher Seite eine Angabe steht.
     */
    record IncomingDocument(String id, String title, String mediaType, byte[] content, List<byte[]> pages,
            String category, LocalDate issuedOn, List<Reference> references) {
        public IncomingDocument {
            pages = List.copyOf(pages);
            category = category == null ? "OTHER" : category;
            references = List.copyOf(references);
        }

        public IncomingDocument(String id, String title, String mediaType, byte[] content, List<byte[]> pages) {
            this(id, title, mediaType, content, pages, "OTHER", null, List.of());
        }
    }

    /** Angabe (Feld-ID des Regelwerks) und Seite, auf der sie im Dokument steht. */
    record Reference(String field, int page) {
    }

    record DocumentInfo(String id, String title, String mediaType, String sha256, int sizeBytes, int pageCount,
            String category, LocalDate issuedOn, List<Reference> references) {
        public DocumentInfo {
            references = List.copyOf(references);
        }
    }

    record DocumentContent(DocumentInfo info, byte[] content) {
    }
}
