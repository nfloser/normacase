package de.normacase.workflow;

import de.normacase.assessment.AssessmentJson;
import de.normacase.assessment.AssessmentService;
import de.normacase.assessment.AssessmentService.Assessment;
import de.normacase.assessment.CaseInput;
import de.normacase.assessment.ExternalReference;
import de.normacase.common.StrictJson;
import de.normacase.documents.DocumentStore;
import de.normacase.documents.DocumentStore.IncomingDocument;
import de.normacase.identity.Actor;
import de.normacase.knowledge.AssessmentOutcome;
import de.normacase.outbound.OutboundAcknowledged;
import de.normacase.outbound.OutboundQueue;
import java.sql.Timestamp;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.context.event.EventListener;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.node.ObjectNode;

/**
 * Vorgänge von der Einlieferung bis zur Rückgabe: Bewertung, Routing in Körbe, Bestätigung, Vier-Augen-Freigabe,
 * Rückgabe und Löschung. Fallinhalte sind befristet; nach Quittung des Fachsystems bleiben nur Metadaten.
 */
@Service
public class WorkflowService {

    private final JdbcClient jdbc;
    private final AssessmentService assessments;
    private final DocumentStore documents;
    private final OutboundQueue outbound;
    private final ApplicationEventPublisher events;
    private final Clock clock;
    private final Duration openRetention;
    private final Duration closedRetention;

    WorkflowService(JdbcClient jdbc, AssessmentService assessments, DocumentStore documents, OutboundQueue outbound,
            ApplicationEventPublisher events, Clock clock,
            @Value("${normacase.workflow.open-retention:P30D}") Duration openRetention,
            @Value("${normacase.workflow.closed-retention:P7D}") Duration closedRetention) {
        this.jdbc = jdbc;
        this.assessments = assessments;
        this.documents = documents;
        this.outbound = outbound;
        this.events = events;
        this.clock = clock;
        this.openRetention = openRetention;
        this.closedRetention = closedRetention;
    }

    public record OpenProcess(String knowledgeRelease, ExternalReference reference, String displayName, CaseInput input,
            List<IncomingDocument> documents) {
    }

    public record ProcessRef(UUID processId, ProcessState state, AssessmentOutcome outcome, UUID evidenceId) {
    }

    @Transactional
    public ProcessRef open(OpenProcess request, Actor actor) {
        Assessment assessment = assessments.assess(request.knowledgeRelease(), request.input(), request.reference(), actor);
        UUID id = UUID.randomUUID();
        Instant now = clock.instant();
        ProcessState state = route(assessment.result().outcome());
        jdbc.sql("""
                insert into workflow.process (process_id, external_system, external_case_id, submitted_by, knowledge_release,
                    state, queue, outcome, case_input, assessment, evidence_id, received_at, updated_at, expires_at)
                values (:id, :system, :caseId, :by, :release, :state, :queue, :outcome, cast(:input as jsonb),
                    cast(:assessment as jsonb), :evidence, :now, :now, :expires)""")
                .param("id", id).param("system", request.reference().system()).param("caseId", request.reference().caseId())
                .param("by", actor.id()).param("release", request.knowledgeRelease()).param("state", state.name())
                .param("queue", state.queue().id()).param("outcome", assessment.result().outcome().name())
                .param("input", StrictJson.write(AssessmentJson.caseInput(request.input())))
                .param("assessment", StrictJson.write(assessment.document()))
                .param("evidence", assessment.evidenceId()).param("now", Timestamp.from(now))
                .param("expires", Timestamp.from(now.plus(openRetention))).update();
        jdbc.sql("insert into workflow.process_subject (process_id, display_name) values (:id, :name)")
                .param("id", id).param("name", request.displayName()).update();
        documents.store(id, request.documents());
        record(id, request.reference(), ProcessEvent.Action.RECEIVED, actor.id(), assessment.result().outcome().name(), null);
        return new ProcessRef(id, state, assessment.result().outcome(), assessment.evidenceId());
    }

    @Transactional
    public ProcessRef clarify(UUID processId, CaseInput input, Actor actor) {
        Row row = require(processId);
        if (row.state() != ProcessState.IN_CLARIFICATION && row.state() != ProcessState.IN_REVIEW) {
            throw new WorkflowException(WorkflowException.Kind.CONFLICT, "Vorgang ist nicht in Bearbeitung");
        }
        Assessment assessment = assessments.assess(row.knowledgeRelease(), input, row.reference(), actor);
        ProcessState state = route(assessment.result().outcome());
        transition(row, state, """
                case_input = cast(:input as jsonb), assessment = cast(:assessment as jsonb), outcome = :outcome,
                evidence_id = :evidence, confirmed_by = null""", q -> q
                .param("input", StrictJson.write(AssessmentJson.caseInput(input)))
                .param("assessment", StrictJson.write(assessment.document()))
                .param("outcome", assessment.result().outcome().name())
                .param("evidence", assessment.evidenceId()));
        record(processId, row.reference(), ProcessEvent.Action.CLARIFIED, actor.id(), assessment.result().outcome().name(), null);
        return new ProcessRef(processId, state, assessment.result().outcome(), assessment.evidenceId());
    }

    @Transactional
    public void confirm(UUID processId, Actor actor) {
        Row row = require(processId);
        expect(row, ProcessState.IN_REVIEW);
        transition(row, ProcessState.AWAITING_APPROVAL, "confirmed_by = :by", q -> q.param("by", actor.id()));
        record(processId, row.reference(), ProcessEvent.Action.CONFIRMED, actor.id(), row.outcome(), null);
    }

    @Transactional
    public void approve(UUID processId, Actor actor) {
        Row row = require(processId);
        expect(row, ProcessState.AWAITING_APPROVAL);
        if (actor.id().equals(row.confirmedBy())) {
            throw new WorkflowException(WorkflowException.Kind.FOUR_EYES, "Freigabe durch eine zweite Person erforderlich");
        }
        transition(row, ProcessState.DISPATCHED, null, q -> q);
        ObjectNode payload = StrictJson.object();
        payload.put("processId", processId.toString());
        payload.put("outcome", row.outcome());
        payload.put("confirmedBy", row.confirmedBy());
        payload.put("approvedBy", actor.id());
        payload.put("evidenceId", row.evidenceId() == null ? null : row.evidenceId().toString());
        payload.set("assessment", StrictJson.parseObject(row.assessmentJson()).node());
        outbound.enqueue(processId, row.submittedBy(), row.reference(), payload);
        record(processId, row.reference(), ProcessEvent.Action.APPROVED, actor.id(), row.outcome(), null);
    }

    @Transactional
    public void reject(UUID processId, RejectionReason reason, Actor actor) {
        Row row = require(processId);
        expect(row, ProcessState.AWAITING_APPROVAL);
        transition(row, ProcessState.IN_REVIEW, "confirmed_by = null", q -> q);
        record(processId, row.reference(), ProcessEvent.Action.REJECTED, actor.id(), row.outcome(), reason.name());
    }

    /** Nach Quittung durch das Fachsystem: Fallinhalte, Identifikationsdaten und Dokumente sofort löschen. */
    @EventListener
    void on(OutboundAcknowledged event) {
        Row row = require(event.processId());
        expect(row, ProcessState.DISPATCHED);
        transition(row, ProcessState.CLOSED, "case_input = null, assessment = null, expires_at = :expires",
                q -> q.param("expires", Timestamp.from(clock.instant().plus(closedRetention))));
        jdbc.sql("delete from workflow.process_subject where process_id = :id").param("id", row.processId()).update();
        documents.deleteAll(row.processId());
        record(row.processId(), row.reference(), ProcessEvent.Action.ACKNOWLEDGED, event.acknowledgedBy(), row.outcome(), null);
    }

    /**
     * Das einliefernde Fachsystem zieht einen Vorgang zurück (Intervenierbarkeit): Inhalte, Identifikationsdaten,
     * Dokumente und nicht abgeholte Ergebnisse werden sofort gelöscht; der Nachweis bleibt.
     */
    @Transactional
    public void withdraw(UUID processId, Actor actor) {
        Row row = find(processId).filter(r -> r.submittedBy().equals(actor.id()))
                .orElseThrow(() -> new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Vorgang nicht gefunden"));
        documents.deleteAll(processId);
        outbound.discard(processId);
        jdbc.sql("delete from workflow.process where process_id = :id").param("id", processId).update();
        record(processId, row.reference(), ProcessEvent.Action.WITHDRAWN, actor.id(), row.outcome(), null);
    }

    /** Löscht abgelaufene Vorgänge vollständig. Mehrere Instanzen sperren sich gegenseitig zeilenweise aus. */
    @Transactional
    public int purgeExpired(Instant now) {
        List<Row> expired = jdbc.sql(SELECT + " where expires_at <= :now order by expires_at limit 500 for update skip locked")
                .param("now", Timestamp.from(now)).query(WorkflowService::row).list();
        for (Row row : expired) {
            documents.deleteAll(row.processId());
            outbound.discard(row.processId());
            jdbc.sql("delete from workflow.process where process_id = :id").param("id", row.processId()).update();
            if (row.state() != ProcessState.CLOSED) {
                record(row.processId(), row.reference(), ProcessEvent.Action.EXPIRED, "system", row.outcome(), null);
            }
        }
        return expired.size();
    }

    public Optional<Row> find(UUID processId) {
        return jdbc.sql(SELECT + " where process_id = :id").param("id", processId).query(WorkflowService::row).optional();
    }

    private Row require(UUID processId) {
        return find(processId).orElseThrow(() -> new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Vorgang nicht gefunden"));
    }

    private static void expect(Row row, ProcessState state) {
        if (row.state() != state) {
            throw new WorkflowException(WorkflowException.Kind.CONFLICT, "Aktion im aktuellen Zustand nicht möglich");
        }
    }

    private static ProcessState route(AssessmentOutcome outcome) {
        return switch (outcome) {
            case INCOMPLETE, HUMAN_REVIEW -> ProcessState.IN_CLARIFICATION;
            case SUPPORTED, NOT_SUPPORTED, NOT_APPLICABLE -> ProcessState.IN_REVIEW;
        };
    }

    private void transition(Row row, ProcessState to, String extraSet,
            java.util.function.UnaryOperator<JdbcClient.StatementSpec> params) {
        String sql = "update workflow.process set state = :to, queue = :queue, updated_at = :now, version = version + 1"
                + (extraSet == null ? "" : ", " + extraSet)
                + " where process_id = :id and version = :version";
        int updated = params.apply(jdbc.sql(sql)
                        .param("to", to.name()).param("queue", to.queue().id()).param("now", Timestamp.from(clock.instant()))
                        .param("id", row.processId()).param("version", row.version()))
                .update();
        if (updated != 1) {
            throw new WorkflowException(WorkflowException.Kind.CONFLICT, "Vorgang wurde zwischenzeitlich geändert");
        }
    }

    private void record(UUID processId, ExternalReference reference, ProcessEvent.Action action, String actor, String outcome,
            String reason) {
        if (action != ProcessEvent.Action.EXPIRED && action != ProcessEvent.Action.WITHDRAWN) {
            jdbc.sql("""
                    insert into workflow.review_event (process_id, at, actor, action, reason)
                    values (:id, :at, :actor, :action, :reason)""")
                    .param("id", processId).param("at", Timestamp.from(clock.instant())).param("actor", actor)
                    .param("action", action.name()).param("reason", reason).update();
        }
        events.publishEvent(new ProcessEvent(processId, reference, action, actor, outcome, reason, clock.instant()));
    }

    static final String SELECT = """
            select process_id, external_system, external_case_id, submitted_by, knowledge_release, state, outcome,
                   assessment::text, evidence_id, confirmed_by, version, received_at, expires_at
            from workflow.process""";

    static Row row(java.sql.ResultSet rs, int n) throws java.sql.SQLException {
        return new Row(rs.getObject(1, UUID.class), new ExternalReference(rs.getString(2), rs.getString(3)), rs.getString(4),
                rs.getString(5), ProcessState.valueOf(rs.getString(6)), rs.getString(7), rs.getString(8),
                rs.getObject(9, UUID.class), rs.getString(10), rs.getInt(11), rs.getTimestamp(12).toInstant(),
                rs.getTimestamp(13).toInstant());
    }

    /** Interne Sicht auf einen Vorgang. {@code assessmentJson} ist nach Abschluss {@code null}. */
    public record Row(UUID processId, ExternalReference reference, String submittedBy, String knowledgeRelease,
            ProcessState state, String outcome, String assessmentJson, UUID evidenceId, String confirmedBy, int version,
            Instant receivedAt, Instant expiresAt) {
    }
}
