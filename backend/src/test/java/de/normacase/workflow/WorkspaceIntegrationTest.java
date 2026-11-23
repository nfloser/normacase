package de.normacase.workflow;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import de.normacase.common.StrictJson;
import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.RequestPostProcessor;
import tools.jackson.databind.JsonNode;

/** Meine Ablage: Ordner, Markierungen und Arbeitslistenfarben je Konto. */
@SpringBootTest
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class WorkspaceIntegrationTest {

    @Autowired
    MockMvc mvc;

    static final RequestPostProcessor OWNER = WorkflowIntegrationTest.as("ablage.a", "gutachter");
    static final RequestPostProcessor OTHER = WorkflowIntegrationTest.as("ablage.b", "freigabe");

    UUID submit(String caseId) throws Exception {
        String response = mvc.perform(post("/api/v1/intake").with(WorkflowIntegrationTest.HOST)
                        .header("Idempotency-Key", "ablage-key-" + caseId).contentType(MediaType.APPLICATION_JSON)
                        .content(WorkflowIntegrationTest.intake("demo-g-supported", caseId, WorkflowIntegrationTest.PDF, "application/pdf")))
                .andExpect(status().isCreated()).andReturn().getResponse().getContentAsString();
        return UUID.fromString(StrictJson.MAPPER.readTree(response).get("processId").stringValue());
    }

    JsonNode send(org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder request, int status) throws Exception {
        String body = mvc.perform(request.with(OWNER)).andExpect(status().is(status)).andReturn().getResponse().getContentAsString();
        return body.isEmpty() ? null : StrictJson.MAPPER.readTree(body);
    }

    String folder(String label, String parent) throws Exception {
        String body = parent == null ? "{\"label\": \"%s\", \"color\": \"#497AB7\"}".formatted(label)
                : "{\"label\": \"%s\", \"color\": \"#497ab7\", \"parentId\": \"%s\"}".formatted(label, parent);
        return send(post("/api/v1/workspace/folders").contentType(MediaType.APPLICATION_JSON).content(body), 201).get("id").stringValue();
    }

    @Test
    void foldersMarksAndQueueColorsBelongToTheAccount() throws Exception {
        UUID a = submit("ABLAGE-1");
        UUID b = submit("ABLAGE-2");
        String top = folder("Rückfragen", null);
        String sub = folder("Diese Woche", top);

        JsonNode state = send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\", \"%s\"], \"command\": \"MOVE\", \"folderId\": \"%s\"}".formatted(a, b, sub)), 200);
        assertThat(state.get("marks")).hasSize(2);
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"COLOR\", \"color\": \"#A54866\"}".formatted(a)), 200);
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"NOTE\", \"note\": \"  Befund folgt  \"}".formatted(a)), 200);
        send(put("/api/v1/workspace/queue-colors/review").contentType(MediaType.APPLICATION_JSON).content("{\"color\": \"#287554\"}"), 204);

        state = send(get("/api/v1/workspace"), 200);
        assertThat(state.at("/folders/0/label").stringValue()).isEqualTo("Diese Woche");
        assertThat(state.at("/folders/1/color").stringValue()).isEqualTo("#497ab7");
        assertThat(state.at("/queueColors/review").stringValue()).isEqualTo("#287554");
        JsonNode markA = null;
        for (JsonNode m : state.get("marks")) {
            if (m.get("processId").stringValue().equals(a.toString())) {
                markA = m;
            }
        }
        assertThat(markA).isNotNull();
        assertThat(markA.get("color").stringValue()).isEqualTo("#a54866");
        assertThat(markA.get("note").stringValue()).isEqualTo("Befund folgt");

        // Andere Konten sehen nichts und können nichts ändern.
        mvc.perform(get("/api/v1/workspace").with(OTHER)).andExpect(status().isOk())
                .andExpect(jsonPath("$.folders.length()").value(0)).andExpect(jsonPath("$.marks.length()").value(0));
        mvc.perform(delete("/api/v1/workspace/folders/" + top).with(OTHER)).andExpect(status().isNotFound());
        mvc.perform(get("/api/v1/workspace").with(WorkflowIntegrationTest.HOST)).andExpect(status().isNotFound());

        // Löschen des oberen Ordners nimmt den Unterordner mit; die Vorgänge sind danach ohne eigenen Ordner.
        send(delete("/api/v1/workspace/folders/" + top), 204);
        state = send(get("/api/v1/workspace"), 200);
        assertThat(state.get("folders")).isEmpty();
        assertThat(state.get("marks")).hasSize(1);
        assertThat(state.at("/marks/0/folderId").isNull()).isTrue();

        // Ohne Farbe, Notiz und Ordner bleibt keine leere Markierung zurück.
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"COLOR\", \"color\": null}".formatted(a)), 200);
        state = send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"NOTE\", \"note\": \"\"}".formatted(a)), 200);
        assertThat(state.get("marks")).isEmpty();
        send(put("/api/v1/workspace/queue-colors/review").contentType(MediaType.APPLICATION_JSON).content("{\"color\": null}"), 204);
        assertThat(send(get("/api/v1/workspace"), 200).get("queueColors").isEmpty()).isTrue();
    }

    @Test
    void invalidInputIsRejected() throws Exception {
        UUID a = submit("ABLAGE-3");
        send(post("/api/v1/workspace/folders").contentType(MediaType.APPLICATION_JSON).content("{\"label\": \" \", \"color\": \"#497ab7\"}"), 400);
        send(post("/api/v1/workspace/folders").contentType(MediaType.APPLICATION_JSON).content("{\"label\": \"X\", \"color\": \"blau\"}"), 400);
        send(post("/api/v1/workspace/folders").contentType(MediaType.APPLICATION_JSON)
                .content("{\"label\": \"X\", \"color\": \"#497ab7\", \"extra\": 1}"), 400);
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"MOVE\", \"folderId\": \"%s\"}".formatted(a, UUID.randomUUID())), 404);
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"BOOKMARK\", \"bookmark\": true}".formatted(UUID.randomUUID())), 404);
        send(post("/api/v1/workspace/marks").contentType(MediaType.APPLICATION_JSON)
                .content("{\"processIds\": [\"%s\"], \"command\": \"NOTE\", \"note\": \"%s\"}".formatted(a, "x".repeat(1001))), 400);
        send(put("/api/v1/workspace/queue-colors/unbekannt").contentType(MediaType.APPLICATION_JSON).content("{\"color\": null}"), 404);

        // Höchstens fünf Ebenen, keine Kreise.
        String parent = null;
        String first = null;
        for (int i = 1; i <= 5; i++) {
            parent = folder("Ebene " + i, parent);
            if (first == null) {
                first = parent;
            }
        }
        send(post("/api/v1/workspace/folders").contentType(MediaType.APPLICATION_JSON)
                .content("{\"label\": \"Ebene 6\", \"color\": \"#497ab7\", \"parentId\": \"%s\"}".formatted(parent)), 400);
        send(put("/api/v1/workspace/folders/" + first).contentType(MediaType.APPLICATION_JSON)
                .content("{\"label\": \"Ebene 1\", \"color\": \"#497ab7\", \"parentId\": \"%s\"}".formatted(parent)), 400);
        send(put("/api/v1/workspace/folders/" + parent).contentType(MediaType.APPLICATION_JSON)
                .content("{\"label\": \"Oben\", \"color\": \"#24708f\"}"), 200);
    }
}
