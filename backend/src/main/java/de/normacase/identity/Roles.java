package de.normacase.identity;

/** Fachliche Rollen aus dem System Design. Die Namen entsprechen den Realm-Rollen im IdP. */
public final class Roles {

    public static final String ASSESSOR = "gutachter";
    public static final String APPROVER = "freigabe";
    public static final String KNOWLEDGE_EDITOR = "regelwerk-redaktion";
    public static final String KNOWLEDGE_APPROVER = "regelwerk-freigabe";
    public static final String ADMINISTRATION = "administration";
    public static final String INTEGRATION = "integration";
    public static final String AUDIT = "revision";

    private Roles() {
    }
}
