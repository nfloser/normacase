package de.normacase.assessment.web;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.jwt;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import de.normacase.common.StrictJson;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.system.CapturedOutput;
import org.springframework.boot.test.system.OutputCaptureExtension;
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
@ExtendWith(OutputCaptureExtension.class)
class AssessmentApiIntegrationTest {

    @Autowired
    MockMvc mvc;

    @Autowired
    JdbcClient jdbc;

    static RequestPostProcessor as(String role, String user) {
        return jwt().jwt(j -> j.claim("preferred_username", user)).authorities(new SimpleGrantedAuthority("ROLE_" + role));
    }

    static String caseFile(String name) throws Exception {
        return Files.readString(Path.of("../legacy/examples/cases/" + name + ".json"));
    }

    static String request(String release, String caseJson, String caseId) {
        return """
                {"knowledgeRelease": "%s", "externalReference": {"system": "host-test", "caseId": "%s"}, "case": %s}"""
                .formatted(release, caseId, caseJson);
    }

    JsonNode assess(String body) throws Exception {
        String response = mvc.perform(post("/api/v1/assessments").with(as("integration", "service-account-host"))
                        .contentType(MediaType.APPLICATION_JSON).content(body))
                .andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString();
        return StrictJson.MAPPER.readTree(response);
    }

    @Test
    void assessesHeadlessAndRecordsEvidenceWithoutContent() throws Exception {
        JsonNode response = assess(request("demo-a-2026.1", caseFile("demo-a-supported"), "VG-1"));

        assertThat(response.at("/document/assessment/outcome").stringValue()).isEqualTo("SUPPORTED");
        assertThat(response.at("/document/formatVersion").intValue()).isEqualTo(2);
        assertThat(response.at("/inputSha256").stringValue()).startsWith("sha256:");

        var row = jdbc.sql("select outcome, actor, actor_kind, external_case_id, rule_id from audit.decision_evidence where evidence_id = cast(:id as uuid)")
                .param("id", response.at("/evidenceId").stringValue()).query().singleRow();
        assertThat(row).containsEntry("outcome", "SUPPORTED").containsEntry("actor", "service-account-host")
                .containsEntry("actor_kind", "SYSTEM").containsEntry("external_case_id", "VG-1")
                .containsEntry("rule_id", "DEMO-A-ELIGIBILITY");
    }

    @Test
    void auditTableHasNoColumnsForCaseContent() {
        List<String> columns = jdbc.sql("""
                select column_name from information_schema.columns
                where table_schema = 'audit' and table_name = 'decision_evidence' order by column_name""")
                .query(String.class).list();
        assertThat(columns).containsExactlyInAnyOrder("evidence_id", "recorded_at", "external_system", "external_case_id",
                "input_sha256", "platform_version", "knowledge_release", "knowledge_sha256", "assessment_date", "rule_id",
                "rule_version", "outcome", "criteria", "actor", "actor_kind");
    }

    @Test
    void criteriaContainResultsButNoValues() throws Exception {
        JsonNode response = assess(request("demo-a-2026.1", caseFile("demo-a-supported"), "VG-2"));
        String criteria = jdbc.sql("select criteria::text from audit.decision_evidence where evidence_id = cast(:id as uuid)")
                .param("id", response.at("/evidenceId").stringValue()).query(String.class).single();
        assertThat(criteria).contains("MATCHED").contains("criterion_a").doesNotContain("TRUTH").doesNotContain("\"YES\"");
    }

    @Test
    void sameInputGivesSameHashAndDocument() throws Exception {
        String body = request("demo-a-2026.1", caseFile("demo-a-supported"), "VG-3");
        JsonNode first = assess(body);
        JsonNode second = assess(body.replace("\n", " "));
        assertThat(second.get("inputSha256")).isEqualTo(first.get("inputSha256"));
        assertThat(second.get("document")).isEqualTo(first.get("document"));
        assertThat(second.get("evidenceId")).isNotEqualTo(first.get("evidenceId"));
    }

    @Test
    void evidenceIsAppendOnly() throws Exception {
        assess(request("demo-a-2026.1", caseFile("demo-a-supported"), "VG-4"));
        assertThatThrownBy(() -> jdbc.sql("update audit.decision_evidence set outcome = 'NOT_SUPPORTED'").update())
                .hasMessageContaining("nur anfügend");
        assertThatThrownBy(() -> jdbc.sql("delete from audit.decision_evidence").update())
                .hasMessageContaining("nur anfügend");
    }

    @Test
    void verificationConfirmsTheOriginalInputAndDetectsChanges() throws Exception {
        String original = caseFile("demo-a-supported");
        String id = assess(request("demo-a-2026.1", original, "VG-5")).at("/evidenceId").stringValue();

        mvc.perform(post("/api/v1/evidence/" + id + "/verification").with(as("revision", "revision.g"))
                        .contentType(MediaType.APPLICATION_JSON).content(original))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.verified").value(true));

        mvc.perform(post("/api/v1/evidence/" + id + "/verification").with(as("revision", "revision.g"))
                        .contentType(MediaType.APPLICATION_JSON).content(original.replace("\"NO\"", "\"YES\"")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.inputMatches").value(false))
                .andExpect(jsonPath("$.verified").value(false));

        mvc.perform(get("/api/v1/evidence").param("system", "host-test").param("caseId", "VG-5").with(as("revision", "revision.g")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[0].outcome").value("SUPPORTED"));
    }

    @Test
    void rolesAreEnforced() throws Exception {
        mvc.perform(post("/api/v1/assessments").with(as("revision", "revision.g"))
                        .contentType(MediaType.APPLICATION_JSON).content(request("demo-a-2026.1", caseFile("demo-a-supported"), "x")))
                .andExpect(status().isForbidden());
        mvc.perform(get("/api/v1/evidence").param("system", "a").param("caseId", "b").with(as("integration", "host")))
                .andExpect(status().isForbidden());
    }

    @Test
    void rejectsUnknownReleaseInvalidDocumentsAndMismatchingCases() throws Exception {
        mvc.perform(post("/api/v1/assessments").with(as("integration", "host")).contentType(MediaType.APPLICATION_JSON)
                        .content(request("gibt-es-nicht", caseFile("demo-a-supported"), "x")))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.type").value("https://normacase.local/problems/unknown-knowledge-release"));

        mvc.perform(post("/api/v1/assessments").with(as("integration", "host")).contentType(MediaType.APPLICATION_JSON)
                        .content("{\"knowledgeRelease\": \"a\", \"knowledgeRelease\": \"b\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.type").value("https://normacase.local/problems/invalid-document"));

        String foreignField = caseFile("demo-a-supported").replace("\"criterion_a\"", "\"fremdes_feld\"");
        mvc.perform(post("/api/v1/assessments").with(as("integration", "host")).contentType(MediaType.APPLICATION_JSON)
                        .content(request("demo-a-2026.1", foreignField, "x")))
                .andExpect(status().isUnprocessableContent())
                .andExpect(jsonPath("$.fields[0]").value("fremdes_feld"));
    }

    @Test
    void caseValuesNeverReachTheLog(CapturedOutput output) throws Exception {
        String release = "pflege-adult-score-2026-10-01-public-reference.1";
        String marker = Files.readString(Path.of("../legacy/examples/document-cases/reference-care-complete/input.json"))
                .replace("\"number\":8", "\"number\":987654321");
        mvc.perform(post("/api/v1/assessments").with(as("integration", "host")).contentType(MediaType.APPLICATION_JSON)
                        .content(request(release, marker, "VG-LOG")))
                .andExpect(status().isOk());
        assertThat(output.getAll()).doesNotContain("987654321");
    }

    @Test
    void listsLoadedReleases() throws Exception {
        mvc.perform(get("/api/v1/knowledge/releases").with(as("gutachter", "gutachterin.a")))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$[?(@.releaseId == 'demo-a-2026.1')].contentSha256").isNotEmpty());
    }
}
