package de.normacase.workflow;

/** Feste Gründe statt Freitext: keine Gesundheitsdaten in Bemerkungen. */
public enum RejectionReason {
    CRITERIA_UNCLEAR,
    EVIDENCE_INSUFFICIENT,
    WRONG_KNOWLEDGE_RELEASE,
    OTHER
}
