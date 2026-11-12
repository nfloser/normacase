package de.normacase.contract;

import static org.junit.jupiter.api.Assertions.*;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.Test;

class ReviewQueueParserTest {
    private final ReviewQueueParser parser = new ReviewQueueParser();
    private String fixture() throws Exception {
        return Files.readString(Path.of("..", "examples", "contracts", "synthetic-review-queue-page.json"));
    }

    @Test void readsSharedSyntheticFixture() throws Exception {
        var page = parser.parse(fixture());
        assertEquals("synthetic-reviewed-queues", page.configurationId());
        assertEquals(1, page.configurationVersion());
        assertNull(page.nextPageCursor());
        var item = page.queues().getFirst().items().getFirst();
        assertEquals("demo-g-review", item.caseId());
        assertEquals("1", item.auditRevision());
        assertFalse(item.batchAllowed());
        assertThrows(UnsupportedOperationException.class, () -> page.queues().clear());
    }
    @Test void preservesUnknownIdentifiersAndLargeRevisionStrings() throws Exception {
        String json = fixture().replace("\"review\"", "\"future-queue\"")
            .replace("\"manual-review\"", "\"future-state\"")
            .replace("\"caseRevision\": \"1\"", "\"caseRevision\": \"90071992547409931234\"")
            .replace("\"nextPageCursor\": null", "\"nextPageCursor\": \"demo-g-review\"");
        var page = parser.parse(json);
        assertEquals("future-queue", page.queues().getFirst().queueId());
        assertEquals("future-state", page.queues().getFirst().items().getFirst().stateId());
        assertEquals("90071992547409931234", page.queues().getFirst().items().getFirst().caseRevision());
        assertEquals("demo-g-review", page.nextPageCursor());
    }
    @Test void rejectsMalformedOrAmbiguousDataWithoutEchoingContent() throws Exception {
        String original = fixture();
        for (String bad : new String[] {
                original.replace("\"caseRevision\": \"1\"", "\"caseRevision\": 1"),
                original.replace("\"batchAllowed\": false", "\"batchAllowed\": null"),
                original.replace("\"nextPageCursor\": null,", ""),
                original.replace("\"configurationVersion\": 1,", "\"configurationVersion\": 1, \"configurationVersion\": 1,"),
                original + " {}", "{ private-data" }) {
            var error = assertThrows(IllegalArgumentException.class, () -> parser.parse(bad));
            assertEquals("Invalid review queue response", error.getMessage());
        }
        assertThrows(IllegalArgumentException.class, () -> parser.parse("x".repeat(1_048_577)));
    }
}
