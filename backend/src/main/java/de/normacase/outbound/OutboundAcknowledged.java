package de.normacase.outbound;

import java.util.UUID;

/** Das Fachsystem hat das Ergebnis übernommen; ab jetzt darf NormaCase den Fallinhalt löschen. */
public record OutboundAcknowledged(UUID messageId, UUID processId, String acknowledgedBy) {
}
