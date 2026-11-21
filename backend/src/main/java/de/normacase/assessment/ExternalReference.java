package de.normacase.assessment;

/** Kennung des Vorgangs im führenden Fachsystem. Pseudonym: der Schlüssel liegt dort. */
public record ExternalReference(String system, String caseId) {
}
