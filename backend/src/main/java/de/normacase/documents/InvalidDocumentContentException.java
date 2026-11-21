package de.normacase.documents;

/** Dokument hat einen nicht erlaubten Typ, passt nicht zu seinem Inhalt oder ist zu groß. */
public class InvalidDocumentContentException extends RuntimeException {

    private final String documentId;

    public InvalidDocumentContentException(String documentId, String message) {
        super(message);
        this.documentId = documentId;
    }

    public String documentId() {
        return documentId;
    }
}
