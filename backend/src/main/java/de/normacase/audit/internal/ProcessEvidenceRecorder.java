package de.normacase.audit.internal;

import de.normacase.workflow.ProcessEvent;
import java.sql.Timestamp;
import java.util.UUID;
import org.springframework.context.event.EventListener;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;

/** Dauerhafter Nachweis jeder Bearbeitungshandlung (wer, was, wann) ohne Fallinhalt. */
@Component
class ProcessEvidenceRecorder {

    private final JdbcClient jdbc;

    ProcessEvidenceRecorder(JdbcClient jdbc) {
        this.jdbc = jdbc;
    }

    @EventListener
    void on(ProcessEvent event) {
        jdbc.sql("""
                insert into audit.process_evidence (evidence_id, recorded_at, process_id, external_system, external_case_id,
                    action, actor, outcome, reason)
                values (:id, :at, :p, :s, :c, :a, :actor, :o, :r)""")
                .param("id", UUID.randomUUID()).param("at", Timestamp.from(event.at())).param("p", event.processId())
                .param("s", event.reference().system()).param("c", event.reference().caseId())
                .param("a", event.action().name()).param("actor", event.actor()).param("o", event.outcome())
                .param("r", event.reason()).update();
    }
}
