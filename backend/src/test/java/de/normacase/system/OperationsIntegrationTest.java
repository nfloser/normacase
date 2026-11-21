package de.normacase.system;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest(properties = "normacase.rate-limit.per-minute=3")
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class OperationsIntegrationTest {

    @Autowired
    MockMvc mvc;

    String request() throws Exception {
        return "{\"knowledgeRelease\": \"demo-a-2026.1\", \"case\": "
                + Files.readString(Path.of("../legacy/examples/cases/demo-a-supported.json")) + "}";
    }

    @Test
    void writesAreRateLimitedPerAccountWithRetryAfter() throws Exception {
        var host = jwt().jwt(j -> j.subject("host-limit")).authorities(new SimpleGrantedAuthority("ROLE_integration"));
        for (int i = 0; i < 3; i++) {
            mvc.perform(post("/api/v1/assessments").with(host).contentType(MediaType.APPLICATION_JSON).content(request()))
                    .andExpect(status().isOk());
        }
        mvc.perform(post("/api/v1/assessments").with(host).contentType(MediaType.APPLICATION_JSON).content(request()))
                .andExpect(status().isTooManyRequests())
                .andExpect(header().exists("Retry-After"))
                .andExpect(jsonPath("$.type").value("https://normacase.local/problems/rate-limited"));

        var other = jwt().jwt(j -> j.subject("host-other")).authorities(new SimpleGrantedAuthority("ROLE_integration"));
        mvc.perform(post("/api/v1/assessments").with(other).contentType(MediaType.APPLICATION_JSON).content(request()))
                .andExpect(status().isOk());
    }

    @Test
    void exposesBusinessMetricsWithoutCaseReferences() throws Exception {
        var host = jwt().jwt(j -> j.subject("host-metrics")).authorities(new SimpleGrantedAuthority("ROLE_integration"));
        mvc.perform(post("/api/v1/assessments").with(host).contentType(MediaType.APPLICATION_JSON)
                .content(request().replace("\"case\"", "\"externalReference\": {\"system\": \"s\", \"caseId\": \"GEHEIM-4711\"}, \"case\"")));
        String metrics = mvc.perform(get("/actuator/prometheus")).andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        assertThat(metrics).contains("normacase_assessments_total").contains("outcome=\"SUPPORTED\"")
                .contains("normacase_processes").contains("normacase_outbound_pending")
                .doesNotContain("GEHEIM-4711").doesNotContain("criterion_a");
    }
}
