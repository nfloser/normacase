package de.normacase.outbound.internal;

import de.normacase.common.Problems;
import de.normacase.common.StrictJson;
import de.normacase.identity.Actor;
import de.normacase.outbound.OutboundAcknowledged;
import java.sql.Timestamp;
import java.time.Clock;
import java.util.UUID;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.http.CacheControl;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.security.core.Authentication;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.node.ArrayNode;
import tools.jackson.databind.node.ObjectNode;

/** Abholschnittstelle: Das Fachsystem holt freigegebene Ergebnisse ab und quittiert sie. */
@RestController
class OutboundController {

    private final JdbcClient jdbc;
    private final ApplicationEventPublisher events;
    private final Clock clock;

    OutboundController(JdbcClient jdbc, ApplicationEventPublisher events, Clock clock) {
        this.jdbc = jdbc;
        this.events = events;
        this.clock = clock;
    }

    @GetMapping(path = "/api/v1/outbound", produces = MediaType.APPLICATION_JSON_VALUE)
    @PreAuthorize("hasRole('integration')")
    ResponseEntity<String> pending(Authentication authentication) {
        ArrayNode list = StrictJson.MAPPER.createArrayNode();
        jdbc.sql("""
                select message_id, external_system, external_case_id, payload::text, created_at from outbound.message
                where recipient = :r and acknowledged_at is null order by created_at limit 100""")
                .param("r", Actor.from(authentication).id())
                .query((rs, n) -> {
                    ObjectNode m = list.addObject();
                    m.put("messageId", rs.getString(1));
                    m.putObject("externalReference").put("system", rs.getString(2)).put("caseId", rs.getString(3));
                    m.set("result", StrictJson.parseObject(rs.getString(4)).node());
                    m.put("createdAt", rs.getTimestamp(5).toInstant().toString());
                    return m;
                }).list();
        return ResponseEntity.ok().cacheControl(CacheControl.noStore()).body(StrictJson.write(list));
    }

    @PostMapping("/api/v1/outbound/{id}/acknowledgement")
    @PreAuthorize("hasRole('integration')")
    @Transactional
    ResponseEntity<?> acknowledge(@PathVariable UUID id, Authentication authentication) {
        String actor = Actor.from(authentication).id();
        var process = jdbc.sql("""
                update outbound.message set acknowledged_at = :at, payload = null
                where message_id = :id and recipient = :r and acknowledged_at is null returning process_id""")
                .param("at", Timestamp.from(clock.instant())).param("id", id).param("r", actor)
                .query(UUID.class).optional();
        if (process.isEmpty()) {
            return ResponseEntity.status(HttpStatus.NOT_FOUND).body(Problems.of(HttpStatus.NOT_FOUND, "not-found",
                    "Nicht gefunden", "Keine offene Nachricht mit dieser Kennung."));
        }
        events.publishEvent(new OutboundAcknowledged(id, process.get(), actor));
        return ResponseEntity.noContent().build();
    }
}
