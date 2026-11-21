package de.normacase.workflow;

import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.binder.MeterBinder;
import java.time.Clock;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;

/** Betriebskennzahlen der Körbe: Anzahl, Alter des ältesten offenen Vorgangs, überfällige Löschungen. */
@Component
class WorkflowMetrics implements MeterBinder {

    private final JdbcClient jdbc;
    private final Clock clock;

    WorkflowMetrics(JdbcClient jdbc, Clock clock) {
        this.jdbc = jdbc;
        this.clock = clock;
    }

    @Override
    public void bindTo(MeterRegistry registry) {
        for (Queue queue : Queue.values()) {
            Gauge.builder("normacase.processes", () -> count("select count(*) from workflow.process where queue = :q", queue.id()))
                    .tag("queue", queue.id()).description("Vorgänge je Korb").register(registry);
        }
        Gauge.builder("normacase.processes.oldest.age", this::oldestOpenSeconds).baseUnit("seconds")
                .description("Alter des ältesten nicht abgeschlossenen Vorgangs").register(registry);
        Gauge.builder("normacase.processes.overdue", () -> jdbc.sql(
                        "select count(*) from workflow.process where expires_at < :now")
                        .param("now", java.sql.Timestamp.from(clock.instant())).query(Long.class).single())
                .description("Vorgänge, deren Löschfrist überschritten ist").register(registry);
        Gauge.builder("normacase.outbound.pending", () -> jdbc.sql(
                        "select count(*) from outbound.message where acknowledged_at is null").query(Long.class).single())
                .description("Freigegebene Ergebnisse, die noch nicht abgeholt wurden").register(registry);
    }

    private double count(String sql, String queue) {
        return jdbc.sql(sql).param("q", queue).query(Long.class).single();
    }

    private double oldestOpenSeconds() {
        return jdbc.sql("""
                select coalesce(extract(epoch from (:now - min(received_at))), 0) from workflow.process
                where state not in ('DISPATCHED', 'CLOSED')""")
                .param("now", java.sql.Timestamp.from(clock.instant())).query(Double.class).single();
    }
}
