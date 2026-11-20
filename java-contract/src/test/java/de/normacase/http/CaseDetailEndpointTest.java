package de.normacase.http;

import static org.junit.jupiter.api.Assertions.*;
import de.normacase.contract.LoopbackReviewQueueClient;
import de.normacase.contract.LoopbackReviewQueueClient.Failure;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletRequest;

class CaseDetailEndpointTest {
    private static final String KEY = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private static MockHttpServletRequest request(String caseId) {
        var request = new MockHttpServletRequest("GET", "/api/review/work-cases/" + caseId);
        request.addHeader("Authorization", "Bearer " + KEY);
        return request;
    }
    private static LoopbackReviewQueueClient.QueueReadException failure(Failure failure) {
        return new LoopbackReviewQueueClient.QueueReadException(failure);
    }

    @Test void returnsTheUpstreamJsonUnchangedForTheRequestedCase() {
        String json = "{\"caseId\":\"demo-g-review\",\"caseRevision\":\"90071992547409931234\",\"future\":1.2300}";
        var observed = new AtomicReference<List<String>>();
        var endpoint = new CaseDetailEndpoint((key, id) -> { observed.set(List.of(key, id)); return json; });
        var response = endpoint.detail("demo-g-review", request("demo-g-review"));
        assertEquals(200, response.getStatusCode().value());
        assertEquals(json, response.getBody());
        assertEquals(List.of(KEY, "demo-g-review"), observed.get());
    }

    @Test void rejectsMissingOrDuplicateCredentialsQueriesAndMalformedIdsBeforeForwarding() {
        var endpoint = new CaseDetailEndpoint((key, id) -> { fail("Should not forward an invalid request"); return ""; });
        assertEquals(401, endpoint.detail("demo-g-review", new MockHttpServletRequest("GET", "/api/review/work-cases/demo-g-review")).getStatusCode().value());
        var duplicate = request("demo-g-review"); duplicate.addHeader("Authorization", "Bearer " + KEY);
        assertEquals(401, endpoint.detail("demo-g-review", duplicate).getStatusCode().value());
        var query = request("demo-g-review"); query.setQueryString("view=full"); query.addParameter("view", "full");
        assertEquals(400, endpoint.detail("demo-g-review", query).getStatusCode().value());
        for (String id : List.of("", "../work-queues", "demo g", "demo%2Fg", "x".repeat(129), "fall-ä")) {
            var invalid = endpoint.detail(id, request("x"));
            assertEquals(404, invalid.getStatusCode().value(), id);
            assertEquals(Messages.ERROR, invalid.getBody());
        }
    }

    @Test void keepsUnknownAndInaccessibleCasesIndistinguishable() {
        var notFound = failure(Failure.NOT_FOUND);
        var endpoint = new CaseDetailEndpoint((key, id) -> { throw notFound; });
        var unknown = endpoint.detail("not-a-case", request("not-a-case"));
        var inaccessible = endpoint.detail("demo-g-supported", request("demo-g-supported"));
        assertEquals(404, unknown.getStatusCode().value());
        assertEquals(unknown.getStatusCode(), inaccessible.getStatusCode());
        assertEquals(unknown.getBody(), inaccessible.getBody());
        assertEquals(unknown.getHeaders().getContentType(), inaccessible.getHeaders().getContentType());
    }

    @Test void mapsUpstreamFailuresWithoutLeakingDetails() {
        for (var entry : List.of(
                new Object[] {Failure.UNAUTHORIZED, 401}, new Object[] {Failure.FORBIDDEN, 403},
                new Object[] {Failure.INVALID_REQUEST, 404}, new Object[] {Failure.UPSTREAM_FAILURE, 502},
                new Object[] {Failure.INVALID_RESPONSE, 502}, new Object[] {Failure.UNAVAILABLE, 502})) {
            var exception = failure((Failure) entry[0]);
            var response = new CaseDetailEndpoint((key, id) -> { throw exception; }).detail("demo-g-review", request("demo-g-review"));
            assertEquals((int) entry[1], response.getStatusCode().value(), entry[0].toString());
            assertFalse(response.getBody().contains(KEY));
        }
    }
}
