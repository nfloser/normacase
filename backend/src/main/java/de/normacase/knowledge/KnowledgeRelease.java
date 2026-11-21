package de.normacase.knowledge;

/**
 * Ein geladenes, unveränderliches Release mit Inhalts-Hash der Quelldatei und optionalen deutschen Beschriftungen
 * ({@code presentation.de-DE.json}). Beschriftungen beeinflussen nie das Ergebnis.
 */
public record KnowledgeRelease(KnowledgePack pack, String contentSha256, String presentationJson) {

    public String releaseId() {
        return pack.manifest().releaseId();
    }
}
