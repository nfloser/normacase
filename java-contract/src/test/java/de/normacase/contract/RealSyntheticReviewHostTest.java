package de.normacase.contract;

import static org.junit.jupiter.api.Assertions.*;
import java.util.Base64;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.condition.EnabledIfEnvironmentVariable;

/** Only enabled by CI against the isolated, synthetic .NET/PostgreSQL host. */
@EnabledIfEnvironmentVariable(named = "NORMACASE_JAVA_INTEGRATION", matches = "true")
class RealSyntheticReviewHostTest {
    @Test void forwardsIdentityToExistingServerAndRetainsCaseScope() throws Exception {
        String credential = System.getenv("NORMACASE_JAVA_TEST_CREDENTIAL");
        assertNotNull(credential);
        var client = new LoopbackReviewQueueClient(5080);
        var page = client.read(credential, null);
        var ids = page.queues().stream().flatMap(queue -> queue.items().stream())
            .map(ReviewQueuePage.Item::caseId).toList();
        assertEquals(java.util.List.of("demo-g-review"), ids);
        var denied = assertThrows(LoopbackReviewQueueClient.QueueReadException.class,
            () -> client.read(Base64.getEncoder().encodeToString(new byte[32]), null));
        assertEquals(LoopbackReviewQueueClient.Failure.UNAUTHORIZED, denied.failure());
    }
    @Test void springEndpointDelegatesAuthorizationAndRejectsCrossOrigin() throws Exception {
        var http = java.net.http.HttpClient.newHttpClient();
        var endpoint = java.net.URI.create("http://127.0.0.1:5081/api/review/work-queues");
        var missing = http.send(java.net.http.HttpRequest.newBuilder(endpoint).GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(401, missing.statusCode());
        assertEquals("no-store", missing.headers().firstValue("Cache-Control").orElse(""));
        String key = System.getenv("NORMACASE_JAVA_TEST_CREDENTIAL");
        var ok = http.send(java.net.http.HttpRequest.newBuilder(endpoint).header("Authorization", "Bearer " + key).GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(200, ok.statusCode());
        var page = new ReviewQueueParser().parse(ok.body());
        assertEquals(java.util.List.of("demo-g-review"), page.queues().stream().flatMap(q -> q.items().stream()).map(ReviewQueuePage.Item::caseId).toList());
        var wrong = http.send(java.net.http.HttpRequest.newBuilder(endpoint).header("Authorization", "Bearer " + Base64.getEncoder().encodeToString(new byte[32])).GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(401, wrong.statusCode());
        var crossOrigin = http.send(java.net.http.HttpRequest.newBuilder(endpoint).header("Authorization", "Bearer " + key).header("Origin", "https://evil.example").GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(403, crossOrigin.statusCode());
        assertTrue(crossOrigin.headers().firstValue("Access-Control-Allow-Origin").isEmpty());
        var invalidCursor = http.send(java.net.http.HttpRequest.newBuilder(java.net.URI.create(endpoint + "?afterCaseId=invalid")).header("Authorization", "Bearer " + key).GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(400, invalidCursor.statusCode());
        var doubled = http.send(java.net.http.HttpRequest.newBuilder(endpoint).header("Authorization", "Bearer " + key).header("Authorization", "Bearer " + key).GET().build(), java.net.http.HttpResponse.BodyHandlers.ofString());
        assertEquals(401, doubled.statusCode());
    }
}
