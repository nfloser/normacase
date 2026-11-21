package de.normacase.knowledge.internal;

import static org.assertj.core.api.Assertions.assertThatThrownBy;

import de.normacase.TestcontainersConfiguration;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.jdbc.core.simple.JdbcClient;

@SpringBootTest
@Import(TestcontainersConfiguration.class)
class KnowledgeReleaseImmutabilityTest {

    @Autowired
    JdbcClient jdbc;

    @Test
    void refusesToLoadAReleaseWhoseContentChanged() {
        jdbc.sql("update knowledge.release set content_sha256 = 'sha256:verfaelscht' where release_id = 'demo-b-2026.1'").update();
        try {
            assertThatThrownBy(() -> new FileKnowledgeCatalog(Path.of("../knowledge"), jdbc))
                    .hasMessageContaining("demo-b-2026.1").hasMessageContaining("unveränderlich");
        } finally {
            jdbc.sql("delete from knowledge.release where release_id = 'demo-b-2026.1'").update();
        }
    }
}
