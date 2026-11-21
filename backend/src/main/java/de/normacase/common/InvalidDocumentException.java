package de.normacase.common;

/** Ein Austauschdokument ist syntaktisch oder strukturell ungültig. Meldung ohne Inhaltswerte. */
public class InvalidDocumentException extends RuntimeException {

    private final String path;

    public InvalidDocumentException(String path, String message) {
        super(message + " (" + path + ")");
        this.path = path;
    }

    public String path() {
        return path;
    }
}
