package de.normacase.workflow;

import de.normacase.assessment.ExternalReference;
import java.time.Instant;
import java.util.UUID;

/** Bearbeitungshandlung für den dauerhaften Nachweis; ohne Fallinhalt. */
public record ProcessEvent(UUID processId, ExternalReference reference, Action action, String actor, String outcome,
        String reason, Instant at) {

    public enum Action { RECEIVED, CLARIFIED, CONFIRMED, APPROVED, REJECTED, ACKNOWLEDGED, WITHDRAWN, EXPIRED }
}
