package de.normacase.knowledge.internal;

import de.normacase.knowledge.KnowledgeCatalog;
import java.time.Duration;
import java.util.List;
import org.springframework.http.CacheControl;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
class KnowledgeController {

    private final KnowledgeCatalog catalog;

    KnowledgeController(KnowledgeCatalog catalog) {
        this.catalog = catalog;
    }

    @GetMapping("/api/v1/knowledge/releases")
    ResponseEntity<List<ReleaseSummary>> releases() {
        List<ReleaseSummary> body = catalog.releases().stream().map(r -> {
            var m = r.pack().manifest();
            return new ReleaseSummary(m.releaseId(), m.packId(), m.lifecycleStatus(), m.validationLevel(), m.entryRuleId(),
                    r.contentSha256(), r.pack().fields().stream().map(f -> new FieldSummary(f.id(), f.type(), f.required())).toList(),
                    r.pack().evidenceRequirements().stream().map(e -> e.id()).toList());
        }).toList();
        return ResponseEntity.ok().cacheControl(CacheControl.maxAge(Duration.ofMinutes(5)).cachePrivate()).body(body);
    }

    @GetMapping(path = "/api/v1/knowledge/releases/{releaseId}/presentation", produces = "application/json")
    ResponseEntity<String> presentation(@org.springframework.web.bind.annotation.PathVariable String releaseId) {
        return catalog.release(releaseId)
                .map(r -> ResponseEntity.ok().cacheControl(CacheControl.maxAge(Duration.ofMinutes(5)).cachePrivate())
                        .body(r.presentationJson() == null ? "{}" : r.presentationJson()))
                .orElseGet(() -> ResponseEntity.status(404).contentType(org.springframework.http.MediaType.APPLICATION_PROBLEM_JSON)
                        .body("{\"type\":\"https://normacase.local/problems/not-found\",\"title\":\"Nicht gefunden\",\"status\":404}"));
    }

    /** Synthetisches Beispiel als Falleingabe, zum Laden in der Prüfwerkstatt und für die Referenzfälle. */
    @GetMapping(path = "/api/v1/knowledge/releases/{releaseId}/examples/{exampleId}", produces = "application/json")
    ResponseEntity<String> example(@org.springframework.web.bind.annotation.PathVariable String releaseId,
            @org.springframework.web.bind.annotation.PathVariable String exampleId) {
        return catalog.example(releaseId, exampleId)
                .map(json -> ResponseEntity.ok().cacheControl(CacheControl.maxAge(Duration.ofMinutes(5)).cachePrivate()).body(json))
                .orElseGet(() -> ResponseEntity.status(404).contentType(org.springframework.http.MediaType.APPLICATION_PROBLEM_JSON)
                        .body("{\"type\":\"https://normacase.local/problems/not-found\",\"title\":\"Nicht gefunden\",\"status\":404}"));
    }

    record ReleaseSummary(String releaseId, String packId, String lifecycleStatus, String validationLevel,
            String entryRuleId, String contentSha256, List<FieldSummary> fields, List<String> evidenceRequirements) {
    }

    record FieldSummary(String id, String type, boolean required) {
    }
}
