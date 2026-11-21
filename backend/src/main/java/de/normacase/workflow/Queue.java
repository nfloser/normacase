package de.normacase.workflow;

/** Arbeitskörbe der Prüfwerkstatt. */
public enum Queue {
    CLARIFICATION("clarification"),
    REVIEW("review"),
    APPROVAL("approval"),
    DONE("done");

    private final String id;

    Queue(String id) {
        this.id = id;
    }

    public String id() {
        return id;
    }

    public static Queue fromId(String id) {
        for (Queue q : values()) {
            if (q.id.equals(id)) {
                return q;
            }
        }
        throw new IllegalArgumentException("Unbekannter Korb");
    }
}
