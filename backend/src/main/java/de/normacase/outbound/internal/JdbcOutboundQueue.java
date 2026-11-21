package de.normacase.outbound.internal;

import de.normacase.assessment.ExternalReference;
import de.normacase.common.StrictJson;
import de.normacase.outbound.OutboundQueue;
import java.sql.Timestamp;
import java.time.Clock;
import java.util.UUID;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;
import tools.jackson.databind.node.ObjectNode;

@Component
class JdbcOutboundQueue implements OutboundQueue {

    private final JdbcClient jdbc;
    private final Clock clock;

    JdbcOutboundQueue(JdbcClient jdbc, Clock clock) {
        this.jdbc = jdbc;
        this.clock = clock;
    }

    @Override
    public UUID enqueue(UUID processId, String recipient, ExternalReference reference, ObjectNode payload) {
        UUID id = UUID.randomUUID();
        jdbc.sql("""
                insert into outbound.message (message_id, process_id, recipient, external_system, external_case_id, payload, created_at)
                values (:id, :p, :r, :s, :c, cast(:payload as jsonb), :at)""")
                .param("id", id).param("p", processId).param("r", recipient).param("s", reference.system())
                .param("c", reference.caseId()).param("payload", StrictJson.write(payload))
                .param("at", Timestamp.from(clock.instant())).update();
        return id;
    }

    @Override
    public void discard(UUID processId) {
        jdbc.sql("update outbound.message set payload = null where process_id = :p").param("p", processId).update();
    }
}
