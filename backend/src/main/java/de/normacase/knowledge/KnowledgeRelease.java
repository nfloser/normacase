package de.normacase.knowledge;

/** Ein geladenes, unveränderliches Release mit Inhalts-Hash der Quelldatei. */
public record KnowledgeRelease(KnowledgePack pack, String contentSha256) {

    public String releaseId() {
        return pack.manifest().releaseId();
    }
}
