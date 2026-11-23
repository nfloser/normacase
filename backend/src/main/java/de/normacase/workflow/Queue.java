package de.normacase.workflow;

/**
 * Arbeitslisten der Prüfwerkstatt wie im bisherigen Fallexplorer: eindeutig bewertet, Angaben fehlen, Gegenprüfung,
 * technische Klärung und reine Unterlagensichtung; danach Freigabe und Erledigt.
 */
public enum Queue {
    REVIEW("review"),
    CLARIFICATION("clarification"),
    CONFLICT("conflict"),
    TECHNICAL("technical"),
    DOCUMENTS("documents"),
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
