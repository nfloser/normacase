package de.normacase.audit.internal;

import de.normacase.assessment.AssessmentJson;
import de.normacase.assessment.AssessmentService;
import de.normacase.common.Problems;
import de.normacase.common.StrictJson;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.http.CacheControl;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

/**
 * Nachvollziehen ohne gespeicherten Fallinhalt: Revision sieht den Nachweis und kann eine Bewertung prüfen,
 * indem das Fachsystem die Originaleingabe erneut einreicht.
 */
@RestController
class EvidenceController {

    private final JdbcClient jdbc;
    private final AssessmentService assessments;

    EvidenceController(JdbcClient jdbc, AssessmentService assessments) {
        this.jdbc = jdbc;
        this.assessments = assessments;
    }

    @GetMapping("/api/v1/evidence")
    @PreAuthorize("hasAnyRole('revision', 'gutachter')")
    ResponseEntity<List<Evidence>> search(@RequestParam String system, @RequestParam String caseId) {
        List<Evidence> found = jdbc.sql(SELECT + " where external_system = :system and external_case_id = :caseId order by recorded_at")
                .param("system", system).param("caseId", caseId).query(Evidence.class).list();
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(found);
    }

    @GetMapping("/api/v1/evidence/{id}")
    @PreAuthorize("hasAnyRole('revision', 'gutachter')")
    ResponseEntity<?> get(@PathVariable UUID id) {
        return find(id).<ResponseEntity<?>>map(e -> ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(e))
                .orElseGet(EvidenceController::notFound);
    }

    @PostMapping(path = "/api/v1/evidence/{id}/verification", consumes = MediaType.APPLICATION_JSON_VALUE)
    @PreAuthorize("hasAnyRole('revision', 'gutachter')")
    ResponseEntity<?> verify(@PathVariable UUID id, @RequestBody String caseJson) {
        Optional<Evidence> evidence = find(id);
        if (evidence.isEmpty()) {
            return notFound();
        }
        Evidence e = evidence.get();
        var input = AssessmentJson.readCase(StrictJson.parseObject(caseJson));
        var replay = assessments.replay(e.knowledgeRelease(), input);
        boolean inputMatches = replay.inputSha256().equals(e.inputSha256());
        boolean knowledgeMatches = replay.knowledgeSha256().equals(e.knowledgeSha256());
        boolean outcomeMatches = replay.result().outcome().name().equals(e.outcome());
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(new Verification(inputMatches,
                knowledgeMatches, outcomeMatches, e.outcome(), replay.result().outcome().name(),
                inputMatches && knowledgeMatches && outcomeMatches));
    }

    private Optional<Evidence> find(UUID id) {
        return jdbc.sql(SELECT + " where evidence_id = :id").param("id", id).query(Evidence.class).optional();
    }

    private static ResponseEntity<ProblemDetail> notFound() {
        return ResponseEntity.status(HttpStatus.NOT_FOUND)
                .body(Problems.of(HttpStatus.NOT_FOUND, "not-found", "Nicht gefunden", "Kein Nachweis mit dieser Kennung."));
    }

    private static final String SELECT = """
            select evidence_id, recorded_at, external_system, external_case_id, input_sha256, platform_version,
                   knowledge_release, knowledge_sha256, assessment_date, rule_id, rule_version, outcome, actor, actor_kind
            from audit.decision_evidence""";

    record Evidence(UUID evidenceId, OffsetDateTime recordedAt, String externalSystem, String externalCaseId,
            String inputSha256, String platformVersion, String knowledgeRelease, String knowledgeSha256,
            LocalDate assessmentDate, String ruleId, Integer ruleVersion, String outcome, String actor, String actorKind) {
    }

    record Verification(boolean inputMatches, boolean knowledgeMatches, boolean outcomeMatches, String recordedOutcome,
            String replayedOutcome, boolean verified) {
    }
}
