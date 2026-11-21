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

    record ReleaseSummary(String releaseId, String packId, String lifecycleStatus, String validationLevel,
            String entryRuleId, String contentSha256, List<FieldSummary> fields, List<String> evidenceRequirements) {
    }

    record FieldSummary(String id, String type, boolean required) {
    }
}
