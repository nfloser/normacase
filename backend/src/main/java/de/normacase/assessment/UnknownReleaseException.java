package de.normacase.assessment;

public class UnknownReleaseException extends RuntimeException {

    public UnknownReleaseException(String releaseId) {
        super("Unbekanntes Knowledge Release: " + releaseId);
    }
}
