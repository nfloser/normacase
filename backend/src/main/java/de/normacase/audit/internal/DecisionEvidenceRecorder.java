package de.normacase.audit.internal;

import de.normacase.assessment.AssessmentCompleted;
import java.sql.Timestamp;
import org.springframework.context.event.EventListener;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;
import tools.jackson.databind.json.JsonMapper;

/**
 * Schreibt den Entscheidungsnachweis in derselben Transaktion wie die Bewertung: keine Bewertung ohne Nachweis.
 */
@Component
class DecisionEvidenceRecorder {

    private final JdbcClient jdbc;
    private final JsonMapper json;

    DecisionEvidenceRecorder(JdbcClient jdbc, JsonMapper json) {
        this.jdbc = jdbc;
        this.json = json;
    }

    @EventListener
    void on(AssessmentCompleted event) {
        jdbc.sql("""
                insert into audit.decision_evidence (evidence_id, recorded_at, external_system, external_case_id,
                    input_sha256, platform_version, knowledge_release, knowledge_sha256, assessment_date, rule_id,
                    rule_version, outcome, criteria, actor, actor_kind)
                values (:id, :at, :system, :caseId, :input, :platform, :release, :knowledge, :date, :rule, :ruleVersion,
                    :outcome, cast(:criteria as jsonb), :actor, :actorKind)""")
                .param("id", event.evidenceId())
                .param("at", Timestamp.from(event.recordedAt()))
                .param("system", event.externalReference() == null ? null : event.externalReference().system())
                .param("caseId", event.externalReference() == null ? null : event.externalReference().caseId())
                .param("input", event.inputSha256())
                .param("platform", event.platformVersion())
                .param("release", event.knowledgeRelease())
                .param("knowledge", event.knowledgeSha256())
                .param("date", event.assessmentDate())
                .param("rule", event.ruleId())
                .param("ruleVersion", event.ruleVersion())
                .param("outcome", event.outcome().name())
                .param("criteria", json.writeValueAsString(event.criteria()))
                .param("actor", event.actor().id())
                .param("actorKind", event.actor().kind().name())
                .update();
    }
}
