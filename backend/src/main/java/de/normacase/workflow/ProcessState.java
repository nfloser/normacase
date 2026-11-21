package de.normacase.workflow;

/** Bearbeitungszustand, getrennt vom Bewertungsergebnis. */
public enum ProcessState {
    IN_CLARIFICATION(Queue.CLARIFICATION),
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
