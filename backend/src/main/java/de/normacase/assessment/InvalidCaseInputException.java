package de.normacase.assessment;

import java.util.List;

/** Die Eingabe passt nicht zum Regelwerk (unbekannte Felder, falsche Typen, unbekannte Belege). */
public class InvalidCaseInputException extends RuntimeException {

    private final List<String> fields;

    public InvalidCaseInputException(String message, List<String> fields) {
        super(message + ": " + String.join(", ", fields));
        this.fields = List.copyOf(fields);
    }

    /** Betroffene Feld-IDs (technische Bezeichner, keine Werte). */
    public List<String> fields() {
        return fields;
    }
}
