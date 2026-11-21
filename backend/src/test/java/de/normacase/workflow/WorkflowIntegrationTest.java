package de.normacase.workflow;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import de.normacase.common.StrictJson;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Instant;
import java.util.Base64;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.RequestPostProcessor;
import tools.jackson.databind.JsonNode;

@SpringBootTest
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class WorkflowIntegrationTest {

    static final String RELEASE = "demo-g-2026.1";
    static final byte[] PDF = "%PDF-1.4\n% synthetisch\n".getBytes(StandardCharsets.US_ASCII);
    static final byte[] PNG = {(byte) 0x89, 'P', 'N', 'G', '\r', '\n', 0x1a, '\n', 0, 0, 0, 0};

    @Autowired
    MockMvc mvc;

    @Autowired
    JdbcClient jdbc;

    @Autowired
    WorkflowService workflow;

    static RequestPostProcessor as(String user, String... roles) {
        return jwt().jwt(j -> j.claim("preferred_username", user))
                .authorities(java.util.Arrays.stream(roles).map(r -> new SimpleGrantedAuthority("ROLE_" + r)).toList());
    }

    static final RequestPostProcessor HOST = as("host-a", "integration");
    static final RequestPostProcessor ASSESSOR = as("gutachterin.a", "gutachter");
    static final RequestPostProcessor APPROVER = as("freigabe.c", "freigabe");

    static String caseFile(String name) throws Exception {
        return Files.readString(Path.of("../legacy/examples/cases/" + name + ".json"));
    }

    static String intake(String caseName, String caseId, byte[] document, String mediaType) throws Exception {
        String b64 = Base64.getEncoder().encodeToString(document);
        return """
                {"knowledgeRelease": "%s", "externalReference": {"system": "host-a", "caseId": "%s"},
                 "subject": {"displayName": "Synthetische Person %s"}, "case": %s,
                 "documents": [{"id": "auftrag", "title": "Prüfauftrag", "mediaType": "%s", "contentBase64": "%s",
                                "pagesBase64": ["%s"]}]}"""
                .formatted(RELEASE, caseId, caseId, caseFile(caseName), mediaType, b64,
                        Base64.getEncoder().encodeToString(PNG));
    }

    UUID submit(String caseName, String caseId) throws Exception {
        String response = mvc.perform(post("/api/v1/intake").with(HOST).header("Idempotency-Key", "key-" + caseId)
                        .contentType(MediaType.APPLICATION_JSON).content(intake(caseName, caseId, PDF, "application/pdf")))
                .andExpect(status().isCreated())
                .andReturn().getResponse().getContentAsString();
        return UUID.fromString(StrictJson.MAPPER.readTree(response).get("processId").stringValue());
    }

    JsonNode detail(UUID id, RequestPostProcessor user) throws Exception {
        return StrictJson.MAPPER.readTree(mvc.perform(get("/api/v1/processes/" + id).with(user))
                .andExpect(status().isOk()).andReturn().getResponse().getContentAsString());
    }

    @Test
    void intakeIsIdempotent() throws Exception {
        String body = intake("demo-g-supported", "IDEM-1", PDF, "application/pdf");
        String first = mvc.perform(post("/api/v1/intake").with(HOST).header("Idempotency-Key", "idem-key-1")
                        .contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isCreated()).andReturn().getResponse().getContentAsString();
        mvc.perform(post("/api/v1/intake").with(HOST).header("Idempotency-Key", "idem-key-1")
                        .contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.replayed").value(true))
                .andExpect(jsonPath("$.processId").value(StrictJson.MAPPER.readTree(first).get("processId").stringValue()));
        mvc.perform(post("/api/v1/intake").with(HOST).header("Idempotency-Key", "idem-key-1")
                        .contentType(MediaType.APPLICATION_JSON).content(body.replace("IDEM-1", "IDEM-2")))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.type").value("https://normacase.local/problems/idempotency-key-reused"));
        mvc.perform(post("/api/v1/intake").with(HOST).contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isBadRequest());
    }

    @Test
    void clearCaseGoesThroughReviewFourEyesApprovalAndIsDeletedAfterAcknowledgement() throws Exception {
        UUID id = submit("demo-g-supported", "FLOW-1");

        JsonNode open = detail(id, ASSESSOR);
        assertThat(open.get("state").stringValue()).isEqualTo("IN_REVIEW");
        assertThat(open.get("outcome").stringValue()).isEqualTo("SUPPORTED");
        assertThat(open.get("displayName").stringValue()).isEqualTo("Synthetische Person FLOW-1");
        assertThat(open.at("/assessment/ruleTrace/ruleId").stringValue()).isNotBlank();
        assertThat(open.at("/documents/0/pageCount").intValue()).isEqualTo(1);
        assertThat(open.get("actions").toString()).contains("confirm");

        mvc.perform(get("/api/v1/processes/" + id + "/documents/auftrag").with(ASSESSOR))
                .andExpect(status().isOk())
                .andExpect(content().contentType("application/pdf"))
                .andExpect(header().string("Content-Security-Policy", "sandbox; default-src 'none'"))
                .andExpect(content().bytes(PDF));
        mvc.perform(get("/api/v1/processes/" + id + "/documents/auftrag/pages/1").with(ASSESSOR))
                .andExpect(status().isOk()).andExpect(content().contentType(MediaType.IMAGE_PNG));

        mvc.perform(post("/api/v1/processes/" + id + "/approval").with(APPROVER)).andExpect(status().isConflict());
        mvc.perform(post("/api/v1/processes/" + id + "/confirmation").with(ASSESSOR)).andExpect(status().isNoContent());
        mvc.perform(post("/api/v1/processes/" + id + "/approval").with(as("gutachterin.a", "gutachter", "freigabe")))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.type").value("https://normacase.local/problems/four-eyes"));
        mvc.perform(post("/api/v1/processes/" + id + "/approval").with(APPROVER)).andExpect(status().isNoContent());

        JsonNode messages = StrictJson.MAPPER.readTree(mvc.perform(get("/api/v1/outbound").with(HOST))
                .andExpect(status().isOk()).andReturn().getResponse().getContentAsString());
        JsonNode message = null;
        for (JsonNode m : messages) {
            if (m.at("/externalReference/caseId").stringValue().equals("FLOW-1")) {
                message = m;
            }
        }
        assertThat(message).isNotNull();
        assertThat(message.at("/result/outcome").stringValue()).isEqualTo("SUPPORTED");
        assertThat(message.at("/result/approvedBy").stringValue()).isEqualTo("freigabe.c");
        assertThat(message.at("/result/assessment/assessment/outcome").stringValue()).isEqualTo("SUPPORTED");

        mvc.perform(get("/api/v1/outbound").with(as("host-b", "integration")))
                .andExpect(status().isOk()).andExpect(jsonPath("$.length()").value(0));
        mvc.perform(post("/api/v1/outbound/" + message.get("messageId").stringValue() + "/acknowledgement")
                        .with(as("host-b", "integration")))
                .andExpect(status().isNotFound());
        mvc.perform(post("/api/v1/outbound/" + message.get("messageId").stringValue() + "/acknowledgement").with(HOST))
                .andExpect(status().isNoContent());

        // Nach der Quittung: keine Fallinhalte, keine Identifikationsdaten, keine Dokumente mehr.
        var row = jdbc.sql("select state, case_input, assessment from workflow.process where process_id = :id")
                .param("id", id).query().singleRow();
        assertThat(row.get("state")).isEqualTo("CLOSED");
        assertThat(row.get("case_input")).isNull();
        assertThat(row.get("assessment")).isNull();
        assertThat(count("select count(*) from workflow.process_subject where process_id = :id", id)).isZero();
        assertThat(count("select count(*) from documents.document where process_id = :id", id)).isZero();
        assertThat(count("select count(*) from documents.page where process_id = :id", id)).isZero();
        assertThat(count("select count(*) from outbound.message where process_id = :id and payload is not null", id)).isZero();

        List<String> actions = jdbc.sql("select action from audit.process_evidence where process_id = :id order by recorded_at")
                .param("id", id).query(String.class).list();
        assertThat(actions).containsExactly("RECEIVED", "CONFIRMED", "APPROVED", "ACKNOWLEDGED");
        assertThat(detail(id, ASSESSOR).get("assessment").isNull()).isTrue();
    }

    @Test
    void incompleteCaseIsClarifiedAndRoutedAgain() throws Exception {
        UUID id = submit("demo-g-incomplete", "CLAR-1");
        assertThat(detail(id, ASSESSOR).get("state").stringValue()).isEqualTo("IN_CLARIFICATION");
        mvc.perform(post("/api/v1/processes/" + id + "/confirmation").with(ASSESSOR)).andExpect(status().isConflict());

        mvc.perform(post("/api/v1/processes/" + id + "/clarification").with(ASSESSOR)
                        .contentType(MediaType.APPLICATION_JSON).content(caseFile("demo-g-supported")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.state").value("IN_REVIEW"))
                .andExpect(jsonPath("$.outcome").value("SUPPORTED"));
    }

    @Test
    void approverCanRejectWithAFixedReason() throws Exception {
        UUID id = submit("demo-g-supported", "REJ-1");
        mvc.perform(post("/api/v1/processes/" + id + "/confirmation").with(ASSESSOR)).andExpect(status().isNoContent());
        mvc.perform(post("/api/v1/processes/" + id + "/rejection").with(APPROVER)
                        .contentType(MediaType.APPLICATION_JSON).content("{\"reason\": \"Freitext mit Diagnose\"}"))
                .andExpect(status().isBadRequest());
        mvc.perform(post("/api/v1/processes/" + id + "/rejection").with(APPROVER)
                        .contentType(MediaType.APPLICATION_JSON).content("{\"reason\": \"EVIDENCE_INSUFFICIENT\"}"))
                .andExpect(status().isNoContent());
        assertThat(detail(id, ASSESSOR).get("state").stringValue()).isEqualTo("IN_REVIEW");
    }

    @Test
    void batchConfirmationReportsEachProcess() throws Exception {
        UUID a = submit("demo-g-supported", "BATCH-1");
        UUID b = submit("demo-g-incomplete", "BATCH-2");
        mvc.perform(post("/api/v1/processes/confirmation").with(ASSESSOR).contentType(MediaType.APPLICATION_JSON)
                        .content("{\"processIds\": [\"" + a + "\", \"" + b + "\", \"kaputt\"]}"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].result").value("CONFIRMED"))
                .andExpect(jsonPath("$[1].result").value("CONFLICT"))
                .andExpect(jsonPath("$[2].result").value("NOT_FOUND"));
    }

    @Test
    void processesAreInvisibleWithoutAWorkbenchRole() throws Exception {
        UUID id = submit("demo-g-supported", "VIS-1");
        mvc.perform(get("/api/v1/processes/" + id).with(as("revision.g", "revision"))).andExpect(status().isNotFound());
        mvc.perform(get("/api/v1/processes/" + id).with(HOST)).andExpect(status().isNotFound());
        mvc.perform(get("/api/v1/processes/" + UUID.randomUUID()).with(ASSESSOR)).andExpect(status().isNotFound());
        mvc.perform(get("/api/v1/processes/" + id + "/documents/auftrag").with(as("revision.g", "revision")))
                .andExpect(status().isNotFound());
        mvc.perform(post("/api/v1/processes/" + id + "/approval").with(ASSESSOR)).andExpect(status().isForbidden());
    }

    @Test
    void documentsMustMatchTheirType() throws Exception {
        mvc.perform(post("/api/v1/intake").with(HOST).header("Idempotency-Key", "key-DOC-1")
                        .contentType(MediaType.APPLICATION_JSON).content(intake("demo-g-supported", "DOC-1", PNG, "application/pdf")))
                .andExpect(status().isUnprocessableContent())
                .andExpect(jsonPath("$.documentId").value("auftrag"));
        assertThat(jdbc.sql("select count(*) from workflow.process where external_case_id = 'DOC-1'").query(Integer.class).single()).isZero();
    }

    @Test
    void expiredProcessesAreDeletedCompletely() throws Exception {
        UUID id = submit("demo-g-supported", "EXP-1");
        workflow.purgeExpired(Instant.now().plus(java.time.Duration.ofDays(31)));
        assertThat(count("select count(*) from workflow.process where process_id = :id", id)).isZero();
        assertThat(count("select count(*) from documents.document where process_id = :id", id)).isZero();
        assertThat(jdbc.sql("select action from audit.process_evidence where process_id = :id order by recorded_at")
                .param("id", id).query(String.class).list()).containsExactly("RECEIVED", "EXPIRED");
    }

    @Test
    void queuesShowCounts() throws Exception {
        submit("demo-g-supported", "Q-1");
        mvc.perform(get("/api/v1/queues").with(ASSESSOR))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[1].id").value("review"))
                .andExpect(jsonPath("$[1].count").isNumber());
        mvc.perform(get("/api/v1/processes").param("queue", "review").with(ASSESSOR))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[?(@.externalReference.caseId == 'Q-1')].displayName").value("Synthetische Person Q-1"));
    }

    int count(String sql, UUID id) {
        return jdbc.sql(sql).param("id", id).query(Integer.class).single();
    }
}
