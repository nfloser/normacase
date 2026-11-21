package de.normacase.intake.internal;

import de.normacase.assessment.AssessmentJson;
import de.normacase.assessment.ExternalReference;
import de.normacase.common.InvalidDocumentException;
import de.normacase.common.Problems;
import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.documents.DocumentStore.IncomingDocument;
import de.normacase.identity.Actor;
import de.normacase.workflow.WorkflowService;
import de.normacase.workflow.WorkflowService.OpenProcess;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.sql.Timestamp;
import java.time.Clock;
import java.util.Base64;
import java.util.HexFormat;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import java.util.regex.Pattern;
import org.springframework.http.CacheControl;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.node.ObjectNode;

/**
 * Generischer Eingang für Fachsysteme. Idempotent über {@code Idempotency-Key}: eine Wiederholung mit gleichem Inhalt
 * liefert denselben Vorgang, ein anderer Inhalt unter gleichem Schlüssel wird abgelehnt.
 */
@RestController
class IntakeController {

    private static final Pattern KEY = Pattern.compile("[A-Za-z0-9._:-]{8,128}");

    private final JdbcClient jdbc;
    private final WorkflowService workflow;
    private final Clock clock;

    IntakeController(JdbcClient jdbc, WorkflowService workflow, Clock clock) {
        this.jdbc = jdbc;
        this.workflow = workflow;
        this.clock = clock;
    }

    @PostMapping(path = "/api/v1/intake", consumes = MediaType.APPLICATION_JSON_VALUE, produces = MediaType.APPLICATION_JSON_VALUE)
    @PreAuthorize("hasRole('integration')")
    @Transactional
    ResponseEntity<?> submit(@RequestHeader(name = "Idempotency-Key", required = false) String key, @RequestBody String body,
            Authentication authentication) {
        if (key == null || !KEY.matcher(key).matches()) {
            return ResponseEntity.badRequest().body(Problems.of(HttpStatus.BAD_REQUEST, "idempotency-key-required",
                    "Idempotency-Key fehlt", "Jede Einlieferung braucht einen Idempotency-Key (8–128 Zeichen)."));
        }
        Actor actor = Actor.from(authentication);
        String requestHash = sha256(body.getBytes(StandardCharsets.UTF_8));
        jdbc.sql("select pg_advisory_xact_lock(hashtext(:k))").param("k", actor.id() + "|" + key).query().singleRow();
        Optional<Receipt> previous = jdbc.sql("""
                select request_sha256, process_id from intake.receipt where submitted_by = :by and idempotency_key = :k""")
                .param("by", actor.id()).param("k", key)
                .query((rs, n) -> new Receipt(rs.getString(1), rs.getObject(2, UUID.class))).optional();
        if (previous.isPresent()) {
            if (!previous.get().requestSha256().equals(requestHash)) {
                return ResponseEntity.status(HttpStatus.CONFLICT).body(Problems.of(HttpStatus.CONFLICT,
                        "idempotency-key-reused", "Schlüssel bereits verwendet",
                        "Unter diesem Idempotency-Key wurde ein anderer Inhalt eingeliefert."));
            }
            return response(HttpStatus.OK, previous.get().processId(), true);
        }

        var ref = workflow.open(parse(body), actor);
        jdbc.sql("""
                insert into intake.receipt (submitted_by, idempotency_key, request_sha256, process_id, received_at)
                values (:by, :k, :h, :p, :at)""")
                .param("by", actor.id()).param("k", key).param("h", requestHash).param("p", ref.processId())
                .param("at", Timestamp.from(clock.instant())).update();
        return response(HttpStatus.CREATED, ref.processId(), false);
    }

    /** Rückzug durch das Fachsystem: löscht den Vorgang sofort. Fremde Vorgänge sind nicht sichtbar (404). */
    @org.springframework.web.bind.annotation.DeleteMapping("/api/v1/intake/{processId}")
    @PreAuthorize("hasRole('integration')")
    ResponseEntity<Void> withdraw(@org.springframework.web.bind.annotation.PathVariable UUID processId, Authentication authentication) {
        workflow.withdraw(processId, Actor.from(authentication));
        return ResponseEntity.noContent().build();
    }

    private ResponseEntity<String> response(HttpStatus status, UUID processId, boolean replayed) {
        var row = workflow.find(processId);
        ObjectNode o = StrictJson.object();
        o.put("processId", processId.toString());
        o.put("replayed", replayed);
        row.ifPresent(r -> {
            o.put("state", r.state().name());
            o.put("outcome", r.outcome());
            o.put("evidenceId", r.evidenceId() == null ? null : r.evidenceId().toString());
        });
        return ResponseEntity.status(status).contentType(MediaType.APPLICATION_JSON).cacheControl(CacheControl.noStore())
                .body(StrictJson.write(o));
    }

    static OpenProcess parse(String body) {
        Obj root = StrictJson.parseObject(body)
                .allow(Set.of("knowledgeRelease", "externalReference", "subject", "case", "documents"));
        Obj ref = root.object("externalReference").allow(Set.of("system", "caseId"));
        Obj subject = root.object("subject").allow(Set.of("displayName"));
        String displayName = subject.string("displayName");
        if (displayName.isBlank() || displayName.length() > 200) {
            throw new InvalidDocumentException(subject.path() + ".displayName", "Anzeigename fehlt oder ist zu lang");
        }
        List<IncomingDocument> documents = root.list("documents", (node, path) -> {
            Obj d = Obj.of(node, path).allow(Set.of("id", "title", "mediaType", "contentBase64", "pagesBase64"));
            return new IncomingDocument(d.string("id"), d.string("title"), d.string("mediaType"),
                    base64(d.string("contentBase64"), path + ".contentBase64"),
                    d.list("pagesBase64", (p, pp) -> base64(p.isString() ? p.stringValue() : null, pp)));
        });
        return new OpenProcess(root.string("knowledgeRelease"),
                new ExternalReference(ref.string("system"), ref.string("caseId")), displayName,
                AssessmentJson.readCase(root.object("case")), documents);
    }

    private static byte[] base64(String value, String path) {
        try {
            return Base64.getDecoder().decode(value == null ? "" : value);
        } catch (IllegalArgumentException e) {
            throw new InvalidDocumentException(path, "Base64 erwartet");
        }
    }

    private static String sha256(byte[] bytes) {
        try {
            return "sha256:" + HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(bytes));
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }

    record Receipt(String requestSha256, UUID processId) {
    }
}
