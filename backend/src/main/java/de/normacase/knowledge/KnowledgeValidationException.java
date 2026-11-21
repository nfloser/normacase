package de.normacase.knowledge;

import java.util.List;

/** Ein Knowledge Pack ist unvollständig, widersprüchlich oder nicht auswertbar. */
public class KnowledgeValidationException extends RuntimeException {

    private final List<Error> errors;

    public KnowledgeValidationException(List<Error> errors) {
        super(errors.size() == 1 ? errors.getFirst().toString() : errors.size() + " Validierungsfehler: " + errors);
        this.errors = List.copyOf(errors);
    }

    public List<Error> errors() {
        return errors;
    }

    /** Stabiler Code, technische Meldung und JSON-Pfad. */
    public record Error(String code, String message, String path) {
        @Override
        public String toString() {
            return code + " at " + path;
        }
    }
}
