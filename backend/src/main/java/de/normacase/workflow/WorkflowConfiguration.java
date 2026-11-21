package de.normacase.workflow;

import java.time.Clock;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;
import org.springframework.scheduling.annotation.Scheduled;

/** Fristablauf: befristete Vorgänge werden regelmäßig vollständig gelöscht. */
@Configuration(proxyBeanMethods = false)
@EnableScheduling
class WorkflowConfiguration {

    private final WorkflowService workflow;
    private final Clock clock;

    WorkflowConfiguration(WorkflowService workflow, Clock clock) {
        this.workflow = workflow;
        this.clock = clock;
    }

    @Scheduled(fixedDelayString = "${normacase.workflow.purge-interval:PT5M}", initialDelayString = "PT1M")
    void purgeExpired() {
        workflow.purgeExpired(clock.instant());
    }
}
