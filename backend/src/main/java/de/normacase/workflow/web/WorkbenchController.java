package de.normacase.workflow.web;

import de.normacase.assessment.AssessmentJson;
import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.documents.DocumentStore;
import de.normacase.identity.Actor;
import de.normacase.identity.Roles;
import de.normacase.workflow.ProcessState;
import de.normacase.workflow.Queue;
import de.normacase.workflow.RejectionReason;
import de.normacase.workflow.WorkflowException;
import de.normacase.workflow.WorkflowService;
import de.normacase.workflow.WorkflowService.Row;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import org.springframework.http.CacheControl;
import org.springframework.http.ContentDisposition;
import org.springframework.http.HttpHeaders;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.node.ArrayNode;
import tools.jackson.databind.node.ObjectNode;

/**
 * Prüfwerkstatt: Körbe, Vorgangsliste, Fallakte und Aktionen. Ohne passende Rolle ist ein Vorgang nicht sichtbar;
 * die Antwort ist dann dieselbe wie für einen nicht vorhandenen Vorgang (404).
 */
@RestController
class WorkbenchController {

    private final JdbcClient jdbc;
    private final WorkflowService workflow;
    private final DocumentStore documents;

    WorkbenchController(JdbcClient jdbc, WorkflowService workflow, DocumentStore documents) {
        this.jdbc = jdbc;
        this.workflow = workflow;
        this.documents = documents;
    }

    @GetMapping(path = "/api/v1/queues", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> queues(Authentication auth) {
        requireReader(auth);
        ArrayNode list = StrictJson.MAPPER.createArrayNode();
        for (Queue q : Queue.values()) {
            int count = jdbc.sql("select count(*) from workflow.process where queue = :q").param("q", q.id()).query(Integer.class).single();
            list.addObject().put("id", q.id()).put("count", count);
        }
        return json(list);
    }

    @GetMapping(path = "/api/v1/processes", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> list(@RequestParam String queue, Authentication auth) {
        requireReader(auth);
        Queue q;
        try {
            q = Queue.fromId(queue);
        } catch (IllegalArgumentException e) {
            throw new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Unbekannter Korb");
        }
        ArrayNode list = StrictJson.MAPPER.createArrayNode();
        jdbc.sql("""
                select p.process_id, p.external_system, p.external_case_id, p.knowledge_release, p.state, p.outcome,
                       p.received_at, p.expires_at, s.display_name, p.confirmed_by,
                       (select count(*) from documents.document d where d.process_id = p.process_id),
                       jsonb_array_length(coalesce(p.assessment->'assessment'->'missingRequiredFields', '[]'::jsonb))
                from workflow.process p left join workflow.process_subject s on s.process_id = p.process_id
                where p.queue = :q order by p.received_at, p.process_id limit 500""")
                .param("q", q.id())
                .query((rs, n) -> {
                    ObjectNode o = list.addObject();
                    o.put("processId", rs.getString(1));
                    o.putObject("externalReference").put("system", rs.getString(2)).put("caseId", rs.getString(3));
                    o.put("knowledgeRelease", rs.getString(4));
                    o.put("state", rs.getString(5));
                    o.put("outcome", rs.getString(6));
                    o.put("receivedAt", rs.getTimestamp(7).toInstant().toString());
                    o.put("expiresAt", rs.getTimestamp(8).toInstant().toString());
                    o.put("displayName", rs.getString(9));
                    o.put("confirmedBy", rs.getString(10));
                    o.put("documentCount", rs.getInt(11));
                    o.put("missingFieldCount", rs.getInt(12));
                    return o;
                }).list();
        return json(list);
    }

    @GetMapping(path = "/api/v1/processes/{id}", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> detail(@PathVariable UUID id, Authentication auth) {
        requireReader(auth);
        Row row = workflow.find(id).orElseThrow(WorkbenchController::notFound);
        ObjectNode o = StrictJson.object();
        o.put("processId", row.processId().toString());
        o.putObject("externalReference").put("system", row.reference().system()).put("caseId", row.reference().caseId());
        o.put("displayName", jdbc.sql("select display_name from workflow.process_subject where process_id = :id")
                .param("id", id).query(String.class).optional().orElse(null));
        o.put("knowledgeRelease", row.knowledgeRelease());
        o.put("state", row.state().name());
        o.put("queue", row.state().queue().id());
        o.put("outcome", row.outcome());
        o.put("evidenceId", row.evidenceId() == null ? null : row.evidenceId().toString());
        o.put("confirmedBy", row.confirmedBy());
        o.put("receivedAt", row.receivedAt().toString());
        o.put("expiresAt", row.expiresAt().toString());
        o.put("technicalError", row.technicalError());
        if (row.assessmentJson() == null) {
            o.putNull("assessment");
        } else {
            o.set("assessment", StrictJson.parseObject(row.assessmentJson()).node());
        }
        String input = jdbc.sql("select case_input::text from workflow.process where process_id = :id").param("id", id)
                .query(String.class).optional().orElse(null);
        if (input == null) {
            o.putNull("caseInput");
        } else {
            o.set("caseInput", StrictJson.parseObject(input).node());
        }
        ArrayNode docs = o.putArray("documents");
        documents.list(id).forEach(d -> {
            ObjectNode doc = docs.addObject().put("id", d.id()).put("title", d.title())
                    .put("mediaType", d.mediaType()).put("sha256", d.sha256()).put("sizeBytes", d.sizeBytes())
                    .put("pageCount", d.pageCount()).put("category", d.category())
                    .put("issuedOn", d.issuedOn() == null ? null : d.issuedOn().toString());
            ArrayNode refs = doc.putArray("references");
            d.references().forEach(r -> refs.addObject().put("field", r.field()).put("page", r.page()));
        });
        ArrayNode history = o.putArray("history");
        jdbc.sql("select at, actor, action, reason from workflow.review_event where process_id = :id order by event_id")
                .param("id", id).query((rs, n) -> history.addObject().put("at", rs.getTimestamp(1).toInstant().toString())
                        .put("actor", rs.getString(2)).put("action", rs.getString(3)).put("reason", rs.getString(4)))
                .list();
        ArrayNode actions = o.putArray("actions");
        allowedActions(row, auth).forEach(actions::add);
        return json(o);
    }

    @GetMapping("/api/v1/processes/{id}/documents/{documentId}")
    ResponseEntity<byte[]> document(@PathVariable UUID id, @PathVariable String documentId, Authentication auth) {
        requireReader(auth);
        var content = documents.content(id, documentId).orElseThrow(WorkbenchController::notFound);
        return ResponseEntity.ok()
                .contentType(MediaType.parseMediaType(content.info().mediaType()))
                .cacheControl(CacheControl.noStore())
                .header(HttpHeaders.CONTENT_DISPOSITION, ContentDisposition.inline().filename(documentId).build().toString())
                .header("Content-Security-Policy", "sandbox; default-src 'none'")
                .header("X-Content-Type-Options", "nosniff")
                .body(content.content());
    }

    @GetMapping("/api/v1/processes/{id}/documents/{documentId}/pages/{page}")
    ResponseEntity<byte[]> page(@PathVariable UUID id, @PathVariable String documentId, @PathVariable int page,
            Authentication auth) {
        requireReader(auth);
        byte[] png = documents.page(id, documentId, page).orElseThrow(WorkbenchController::notFound);
        return ResponseEntity.ok().contentType(MediaType.IMAGE_PNG).cacheControl(CacheControl.noStore())
                .header("X-Content-Type-Options", "nosniff").body(png);
    }

    @PostMapping("/api/v1/processes/{id}/confirmation")
    ResponseEntity<Void> confirm(@PathVariable UUID id, Authentication auth) {
        requireRole(auth, Roles.ASSESSOR);
        workflow.confirm(id, Actor.from(auth));
        return ResponseEntity.noContent().build();
    }

    /** Sammelbestätigung eindeutiger Fälle; jeder Vorgang wird einzeln geprüft und protokolliert. */
    @PostMapping(path = "/api/v1/processes/confirmation", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> confirmBatch(@RequestBody String body, Authentication auth) {
        requireRole(auth, Roles.ASSESSOR);
        Obj request = StrictJson.parseObject(body).allow(Set.of("processIds"));
        List<String> ids = request.list("processIds", (n, p) -> n.isString() ? n.stringValue() : "");
        ArrayNode results = StrictJson.MAPPER.createArrayNode();
        for (String raw : new ArrayList<>(new java.util.LinkedHashSet<>(ids))) {
            String result;
            try {
                workflow.confirm(UUID.fromString(raw), Actor.from(auth));
                result = "CONFIRMED";
            } catch (WorkflowException e) {
                result = e.kind().name();
            } catch (IllegalArgumentException e) {
                result = "NOT_FOUND";
            }
            results.addObject().put("processId", raw).put("result", result);
        }
        return json(results);
    }

    @PostMapping("/api/v1/processes/{id}/approval")
    ResponseEntity<Void> approve(@PathVariable UUID id, Authentication auth) {
        requireRole(auth, Roles.APPROVER);
        workflow.approve(id, Actor.from(auth));
        return ResponseEntity.noContent().build();
    }

    @PostMapping("/api/v1/processes/{id}/rejection")
    ResponseEntity<Void> reject(@PathVariable UUID id, @RequestBody String body, Authentication auth) {
        requireRole(auth, Roles.APPROVER);
        RejectionReason reason = StrictJson.parseObject(body).allow(Set.of("reason")).optEnum("reason", RejectionReason.class);
        if (reason == null) {
            throw new de.normacase.common.InvalidDocumentException("$.reason", "Grund fehlt");
        }
        workflow.reject(id, reason, Actor.from(auth));
        return ResponseEntity.noContent().build();
    }

    @PostMapping(path = "/api/v1/processes/{id}/clarification", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> clarify(@PathVariable UUID id, @RequestBody String body, Authentication auth) {
        requireRole(auth, Roles.ASSESSOR);
        var ref = workflow.clarify(id, AssessmentJson.readCase(body), Actor.from(auth));
        ObjectNode o = StrictJson.object();
        o.put("processId", ref.processId().toString());
        o.put("state", ref.state().name());
        o.put("queue", ref.state().queue().id());
        o.put("outcome", ref.outcome());
        return json(o);
    }

    private static List<String> allowedActions(Row row, Authentication auth) {
        List<String> actions = new ArrayList<>();
        boolean assessor = has(auth, Roles.ASSESSOR);
        boolean approver = has(auth, Roles.APPROVER);
        String me = Actor.from(auth).id();
        if (assessor && WorkflowService.clarifiable(row)) {
            actions.add("clarify");
        }
        if (assessor && row.state() == ProcessState.IN_REVIEW) {
            actions.add("confirm");
        }
        if (approver && row.state() == ProcessState.AWAITING_APPROVAL) {
            if (!me.equals(row.confirmedBy())) {
                actions.add("approve");
            }
            actions.add("reject");
        }
        return actions;
    }

    private static void requireReader(Authentication auth) {
        if (!has(auth, Roles.ASSESSOR) && !has(auth, Roles.APPROVER)) {
            throw notFound();
        }
    }

    private static void requireRole(Authentication auth, String role) {
        requireReader(auth);
        if (!has(auth, role)) {
            throw new org.springframework.security.access.AccessDeniedException("Rolle fehlt");
        }
    }

    private static boolean has(Authentication auth, String role) {
        return auth.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_" + role));
    }

    private static WorkflowException notFound() {
        return new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Nicht gefunden");
    }

    private static ResponseEntity<String> json(tools.jackson.databind.JsonNode node) {
        return ResponseEntity.ok().contentType(MediaType.APPLICATION_JSON).cacheControl(CacheControl.noStore())
                .body(StrictJson.write(node));
    }
}
