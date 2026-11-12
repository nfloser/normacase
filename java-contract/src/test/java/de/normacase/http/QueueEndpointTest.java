package de.normacase.http;

import static org.junit.jupiter.api.Assertions.*;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletRequest;

class QueueEndpointTest {
    private static final String KEY = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private static MockHttpServletRequest request() { return new MockHttpServletRequest("GET", "/api/review/work-queues"); }
    @Test void forwardsOnlyTheSingleCredentialAndCursor() {
        var observed = new AtomicReference<List<String>>();
        var endpoint = new QueueEndpoint((key, cursor) -> { observed.set(List.of(key, cursor)); return "{\"future\":true}"; });
        var request = request(); request.addHeader("Authorization", "Bearer " + KEY); request.addParameter("afterCaseId", "demo-g-review");
        var response = endpoint.queue(request);
        assertEquals(200, response.getStatusCode().value());
        assertEquals("{\"future\":true}", response.getBody());
        assertEquals(List.of(KEY, "demo-g-review"), observed.get());
    }
    @Test void rejectsMissingDuplicateOrMalformedCredentialsAndQueries() {
        var endpoint = new QueueEndpoint((key, cursor) -> { fail("Should not forward an invalid request"); return ""; });
        assertEquals(401, endpoint.queue(request()).getStatusCode().value());
        var duplicate = request(); duplicate.addHeader("Authorization", "Bearer " + KEY); duplicate.addHeader("Authorization", "Bearer " + KEY);
        assertEquals(401, endpoint.queue(duplicate).getStatusCode().value());
        var malformed = request(); malformed.addHeader("Authorization", "Basic " + KEY); assertEquals(401, endpoint.queue(malformed).getStatusCode().value());
        var multiple = request(); multiple.addHeader("Authorization", "Bearer " + KEY); multiple.addParameter("afterCaseId", "one", "two"); assertEquals(400, endpoint.queue(multiple).getStatusCode().value());
        var unknown = request(); unknown.addHeader("Authorization", "Bearer " + KEY); unknown.addParameter("secret", "value"); assertEquals(400, endpoint.queue(unknown).getStatusCode().value());
    }
}
