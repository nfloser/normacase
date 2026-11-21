package de.normacase.assessment.web;

import de.normacase.assessment.AssessmentJson;
import de.normacase.assessment.AssessmentService;
import de.normacase.assessment.AssessmentService.Assessment;
import de.normacase.assessment.ExternalReference;
import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.identity.Actor;
import java.util.Set;
import org.springframework.http.CacheControl;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.node.ObjectNode;

/**
 * Headless-Bewertung für Fachsysteme und Prüfwerkstatt. Der Fall wird bewertet und nicht gespeichert;
 * es bleibt nur der Entscheidungsnachweis.
 */
@RestController
class AssessmentController {

    private final AssessmentService service;

    AssessmentController(AssessmentService service) {
        this.service = service;
    }

    @PostMapping(path = "/api/v1/assessments", consumes = MediaType.APPLICATION_JSON_VALUE,
            produces = MediaType.APPLICATION_JSON_VALUE)
    @PreAuthorize("hasAnyRole('integration', 'gutachter')")
    ResponseEntity<String> assess(@RequestBody String body, Authentication authentication) {
        Obj request = StrictJson.parseObject(body).allow(Set.of("knowledgeRelease", "externalReference", "case"));
        Obj ref = request.optObject("externalReference");
        ExternalReference reference = ref == null ? null
                : new ExternalReference(ref.allow(Set.of("system", "caseId")).string("system"), ref.string("caseId"));
        Assessment assessment = service.assess(request.string("knowledgeRelease"),
                AssessmentJson.readCase(request.object("case")), reference, Actor.from(authentication));

        ObjectNode response = StrictJson.object();
        response.put("evidenceId", assessment.evidenceId().toString());
        response.put("inputSha256", assessment.inputSha256());
        response.put("knowledgeSha256", assessment.knowledgeSha256());
        response.set("document", assessment.document());
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(StrictJson.write(response));
    }
}
