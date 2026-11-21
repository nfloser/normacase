package de.normacase.system;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import de.normacase.TestcontainersConfiguration;
import java.sql.Connection;
import java.util.UUID;
import javax.sql.DataSource;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.jdbc.datasource.DriverManagerDataSource;

/** Das Laufzeitkonto darf Daten bearbeiten, aber kein Schema ändern und keinen Nachweis verändern. */
@SpringBootTest
@Import(TestcontainersConfiguration.class)
class RuntimeRoleIntegrationTest {

    @Autowired
    DataSource owner;

    JdbcClient app;

    @BeforeEach
    void connectAsRuntimeRole() throws Exception {
        JdbcClient.create(owner).sql("alter role normacase_app login password 'laufzeit-test'").update();
        String url;
        try (Connection c = owner.getConnection()) {
            url = c.getMetaData().getURL();
        }
        app = JdbcClient.create(new DriverManagerDataSource(url, "normacase_app", "laufzeit-test"));
    }

    @Test
    void mayWorkWithTemporaryContent() {
        UUID id = UUID.randomUUID();
        app.sql("""
                insert into workflow.process (process_id, external_system, external_case_id, submitted_by, knowledge_release,
                    state, queue, outcome, received_at, updated_at, expires_at)
                values (:id, 's', 'c', 'host', 'r', 'IN_REVIEW', 'review', 'SUPPORTED', now(), now(), now())""")
                .param("id", id).update();
        assertThat(app.sql("update workflow.process set queue = 'approval' where process_id = :id").param("id", id).update()).isOne();
        assertThat(app.sql("delete from workflow.process where process_id = :id").param("id", id).update()).isOne();
        assertThat(app.sql("select count(*) from audit.decision_evidence").query(Long.class).single()).isNotNegative();
    }

    @Test
    void mayNotChangeEvidenceOrRegistersOrSchema() {
        assertThatThrownBy(() -> app.sql("update audit.decision_evidence set outcome = 'X'").update())
                .rootCause().hasMessageContaining("permission denied");
        assertThatThrownBy(() -> app.sql("delete from audit.process_evidence").update())
                .rootCause().hasMessageContaining("permission denied");
        assertThatThrownBy(() -> app.sql("update knowledge.release set content_sha256 = 'x'").update())
                .rootCause().hasMessageContaining("permission denied");
        assertThatThrownBy(() -> app.sql("delete from intake.receipt").update())
                .rootCause().hasMessageContaining("permission denied");
        assertThatThrownBy(() -> app.sql("create table workflow.fremd (id int)").update())
                .rootCause().hasMessageContaining("permission denied");
        assertThatThrownBy(() -> app.sql("drop table workflow.process").update())
                .rootCause().hasMessageContaining("must be owner");
    }
}
