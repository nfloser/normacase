package de.normacase.outbound;

import de.normacase.assessment.ExternalReference;
import java.util.UUID;
import tools.jackson.databind.node.ObjectNode;

/** Freigegebene Ergebnisse für die Abholung durch das Fachsystem bereitstellen. */
public interface OutboundQueue {

    UUID enqueue(UUID processId, String recipient, ExternalReference reference, ObjectNode payload);

    /** Verwirft noch nicht abgeholte Inhalte eines Vorgangs, etwa bei Fristablauf. */
    void discard(UUID processId);
}
