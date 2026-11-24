package de.normacase.workflow;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import de.normacase.TestcontainersConfiguration;
import de.normacase.common.StrictJson;
import java.awt.Color;
import java.awt.Graphics2D;
import java.awt.image.BufferedImage;
import java.io.ByteArrayOutputStream;
import java.util.Base64;
import javax.imageio.IIOImage;
import javax.imageio.ImageIO;
import javax.imageio.ImageWriter;
import javax.imageio.stream.ImageOutputStream;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

/** Fallakte mit verschiedenen Formaten: Dokumentart, Ausstellungsdatum, Herkunft von Angaben und TIFF-Scans. */
@SpringBootTest
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class DocumentIntakeIntegrationTest {

    @Autowired
    MockMvc mvc;

    static byte[] tiff(int pages) throws Exception {
        ImageWriter writer = ImageIO.getImageWritersByFormatName("tiff").next();
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        try (ImageOutputStream stream = ImageIO.createImageOutputStream(out)) {
            writer.setOutput(stream);
            writer.prepareWriteSequence(null);
            for (int i = 0; i < pages; i++) {
                BufferedImage image = new BufferedImage(200, 280, BufferedImage.TYPE_BYTE_GRAY);
                Graphics2D g = image.createGraphics();
                g.setColor(Color.WHITE);
                g.fillRect(0, 0, 200, 280);
                g.setColor(Color.BLACK);
                g.drawString("Seite " + (i + 1), 20, 40);
                g.dispose();
                writer.writeToSequence(new IIOImage(image, null, null), null);
            }
            writer.endWriteSequence();
        }
        writer.dispose();
        return out.toByteArray();
    }

    static String body(String caseId, String documents) throws Exception {
        return """
                {"knowledgeRelease": "%s", "externalReference": {"system": "host-a", "caseId": "%s"},
                 "subject": {"displayName": "Synthetische Person %s"}, "case": %s, "documents": %s}"""
                .formatted(WorkflowIntegrationTest.RELEASE, caseId, caseId, WorkflowIntegrationTest.caseFile("demo-g-supported"), documents);
    }

    @Test
    void storesCategoryDateReferencesAndRendersTiffPages() throws Exception {
        String scan = Base64.getEncoder().encodeToString(tiff(2));
        String text = Base64.getEncoder().encodeToString("Übergabebericht Pflegedienst".getBytes(java.nio.charset.StandardCharsets.UTF_8));
        String documents = """
                [{"id": "fax-scan", "title": "Pflegetagebuch (Fax)", "mediaType": "image/tiff", "contentBase64": "%s",
                  "category": "CARE", "issuedOn": "2026-10-01", "references": [{"field": "criteria_confirmed", "page": 2}]},
                 {"id": "bericht", "title": "Übergabebericht", "mediaType": "text/plain", "contentBase64": "%s",
                  "category": "CARE", "issuedOn": "2026-09-20"}]"""
                .formatted(scan, text);
        String response = mvc.perform(post("/api/v1/intake").with(WorkflowIntegrationTest.HOST)
                        .header("Idempotency-Key", "doc-meta-1").contentType(MediaType.APPLICATION_JSON).content(body("DOC-META-1", documents)))
                .andExpect(status().isCreated()).andReturn().getResponse().getContentAsString();
        String id = StrictJson.MAPPER.readTree(response).get("processId").stringValue();

        mvc.perform(get("/api/v1/processes/" + id).with(WorkflowIntegrationTest.ASSESSOR))
                .andExpect(status().isOk())
                // nach Ausstellungsdatum sortiert
                .andExpect(jsonPath("$.documents[0].id").value("bericht"))
                .andExpect(jsonPath("$.documents[1].id").value("fax-scan"))
                .andExpect(jsonPath("$.documents[1].category").value("CARE"))
                .andExpect(jsonPath("$.documents[1].issuedOn").value("2026-10-01"))
                .andExpect(jsonPath("$.documents[1].pageCount").value(2))
                .andExpect(jsonPath("$.documents[1].references[0].field").value("criteria_confirmed"))
                .andExpect(jsonPath("$.documents[1].references[0].page").value(2));
        byte[] page = mvc.perform(get("/api/v1/processes/" + id + "/documents/fax-scan/pages/2").with(WorkflowIntegrationTest.ASSESSOR))
                .andExpect(status().isOk()).andExpect(content().contentType(MediaType.IMAGE_PNG))
                .andReturn().getResponse().getContentAsByteArray();
        assertThat(ImageIO.read(new java.io.ByteArrayInputStream(page)).getWidth()).isEqualTo(200);
        mvc.perform(get("/api/v1/processes/" + id + "/documents/fax-scan").with(WorkflowIntegrationTest.ASSESSOR))
                .andExpect(status().isOk()).andExpect(content().contentType("image/tiff"));
    }

    @Test
    void rejectsInvalidMetadataAndForgedTiff() throws Exception {
        String scan = Base64.getEncoder().encodeToString(tiff(1));
        String[] invalid = {
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\", \"category\": \"GEHEIM\"}]".formatted(scan),
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\", \"issuedOn\": \"01.10.2026\"}]".formatted(scan),
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\", \"references\": [{\"field\": \"x\", \"page\": 2}]}]".formatted(scan),
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\", \"references\": [{\"field\": \"Name; drop\", \"page\": 1}]}]".formatted(scan),
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\"}]".formatted(
                    Base64.getEncoder().encodeToString("II*\u0000kaputt".getBytes(java.nio.charset.StandardCharsets.ISO_8859_1))),
            "[{\"id\": \"a\", \"title\": \"A\", \"mediaType\": \"image/tiff\", \"contentBase64\": \"%s\", \"pagesBase64\": [\"%s\"]}]".formatted(
                    scan, Base64.getEncoder().encodeToString(WorkflowIntegrationTest.PNG)),
        };
        // Formfehler im JSON: 400; unzulässiger Inhalt eines Dokuments (wie bei PDF und PNG): 422.
        int[] expected = {422, 400, 422, 422, 422, 422};
        for (int i = 0; i < invalid.length; i++) {
            mvc.perform(post("/api/v1/intake").with(WorkflowIntegrationTest.HOST).header("Idempotency-Key", "doc-bad-" + i)
                            .contentType(MediaType.APPLICATION_JSON).content(body("DOC-BAD-" + i, invalid[i])))
                    .andExpect(status().is(expected[i]));
        }
    }
}
