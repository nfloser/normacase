package de.normacase.workflow;

/** Bearbeitungszustand, getrennt vom Bewertungsergebnis. */
public enum ProcessState {
    /** Angaben fehlen ({@code INCOMPLETE}). */
    IN_CLARIFICATION(Queue.CLARIFICATION),
    /** Belege widersprechen sich ({@code HUMAN_REVIEW}): ein Mensch prüft gegen. */
    IN_CONFLICT_REVIEW(Queue.CONFLICT),
    /** Technischer Fehler vor der Bewertung (unbekanntes Regelwerk, Eingabe passt nicht); keine Bewertung. */
    TECHNICAL_CLARIFICATION(Queue.TECHNICAL),
    /** Unterlagen ohne hinterlegtes Regelwerk: nur Sichtung, keine Entscheidung. */
    DOCUMENT_REVIEW(Queue.DOCUMENTS),
    IN_REVIEW(Queue.REVIEW),
    AWAITING_APPROVAL(Queue.APPROVAL),
    DISPATCHED(Queue.DONE),
    CLOSED(Queue.DONE);

    private final Queue queue;

    ProcessState(Queue queue) {
        this.queue = queue;
    }

    public Queue queue() {
        return queue;
    }
}
