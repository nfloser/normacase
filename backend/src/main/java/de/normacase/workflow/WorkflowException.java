package de.normacase.workflow;

public class WorkflowException extends RuntimeException {

    public enum Kind { NOT_FOUND, CONFLICT, FOUR_EYES }

    private final Kind kind;

    public WorkflowException(Kind kind, String message) {
        super(message);
        this.kind = kind;
    }

    public Kind kind() {
        return kind;
    }
}
