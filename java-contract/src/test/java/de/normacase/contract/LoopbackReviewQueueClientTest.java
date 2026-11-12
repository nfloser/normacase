package de.normacase.contract;

import static org.junit.jupiter.api.Assertions.*;
import com.sun.net.httpserver.HttpServer;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Base64;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

class LoopbackReviewQueueClientTest {
    private final String credential = Base64.getEncoder().encodeToString(new byte[32]);
    private HttpServer server;
    private LoopbackReviewQueueClient client;
    private final AtomicInteger requests = new AtomicInteger();
    private final AtomicReference<String> query = new AtomicReference<>();
    private final AtomicReference<String> authorization = new AtomicReference<>();
    private final AtomicBoolean stallBody = new AtomicBoolean();
    private int status;
    private byte[] body;
    private String contentType;

    @BeforeEach void setup() throws Exception {
        status = 200;
        body = Files.readAllBytes(Path.of("..", "examples", "contracts", "synthetic-review-queue-page.json"));
        contentType = "application/json; charset=utf-8";
        server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        server.createContext("/api/review/work-queues", exchange -> {
            requests.incrementAndGet();
            query.set(exchange.getRequestURI().getRawQuery());
            authorization.set(exchange.getRequestHeaders().getFirst("Authorization"));
            exchange.getResponseHeaders().set("Content-Type", contentType);
            if (status == 302) exchange.getResponseHeaders().set("Location", "http://127.0.0.1:" + server.getAddress().getPort() + "/unexpected");
            exchange.sendResponseHeaders(status, body.length);
            try (var output = exchange.getResponseBody()) {
                if (stallBody.get()) {
                    output.write(body, 0, 1); output.flush();
                    try { Thread.sleep(6500); } catch (InterruptedException ex) { Thread.currentThread().interrupt(); }
                    return;
                }
                output.write(body);
            }
        });
        server.start(); client = new LoopbackReviewQueueClient(server.getAddress().getPort());
    }
    @AfterEach void stop() { if (server != null) server.stop(0); }

    @Test void readsAndPreservesScopedUpstreamResponse() throws Exception {
        var page = client.read(credential, "demo-g-review");
        assertEquals("Bearer " + credential, authorization.get());
        assertEquals("afterCaseId=demo-g-review", query.get());
        assertEquals("demo-g-review", page.queues().getFirst().items().getFirst().caseId());
        assertNull(client.read(credential, null).nextPageCursor());
        assertNull(query.get());
    }
    @Test void encodesCursorAndRejectsBadInputsBeforeAnyRequest() throws Exception {
        client.read(credential, "case&other=x");
        assertEquals("afterCaseId=case%26other%3Dx", query.get());
        var error = assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read("bad", null));
        assertEquals(LoopbackReviewQueueClient.Failure.INVALID_REQUEST, error.failure());
        assertEquals(1, requests.get());
        assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, "x".repeat(257)));
        assertEquals(1, requests.get());
        assertFalse(error.getMessage().contains(credential));
    }
    @Test void mapsAuthorizationAndDoesNotFollowRedirects() {
        for (int code : new int[] {401, 403, 302, 500}) {
            status = code;
            var error = assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null));
            assertEquals(code == 401 ? LoopbackReviewQueueClient.Failure.UNAUTHORIZED
                : code == 403 ? LoopbackReviewQueueClient.Failure.FORBIDDEN : LoopbackReviewQueueClient.Failure.UPSTREAM_FAILURE, error.failure());
        }
        assertEquals(4, requests.get());
    }
    @Test void failsClosedForInvalidOrOversizedResponses() {
        body = "{broken".getBytes(StandardCharsets.UTF_8);
        assertEquals(LoopbackReviewQueueClient.Failure.INVALID_RESPONSE,
            assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null)).failure());
        body = new byte[] {(byte) 0xc3, (byte) 0x28};
        assertEquals(LoopbackReviewQueueClient.Failure.INVALID_RESPONSE,
            assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null)).failure());
        contentType = "text/html"; body = "private data".getBytes(StandardCharsets.UTF_8);
        assertEquals(LoopbackReviewQueueClient.Failure.INVALID_RESPONSE,
            assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null)).failure());
        contentType = "application/json"; body = new byte[1_048_577];
        var error = assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null));
        assertEquals(LoopbackReviewQueueClient.Failure.UNAVAILABLE, error.failure());
        assertFalse(error.getMessage().contains("private data"));
    }
    @Test void timesOutWhenAnUpstreamBodyStalls() {
        stallBody.set(true);
        long start = System.nanoTime();
        var error = assertThrows(LoopbackReviewQueueClient.QueueReadException.class, () -> client.read(credential, null));
        assertEquals(LoopbackReviewQueueClient.Failure.UNAVAILABLE, error.failure());
        assertTrue(java.time.Duration.ofNanos(System.nanoTime() - start).toSeconds() < 7);
    }
    @Test void reportsUnreachableServerWithoutLeakingInput() {
        int port = server.getAddress().getPort(); server.stop(0); server = null;
        var error = assertThrows(LoopbackReviewQueueClient.QueueReadException.class,
            () -> new LoopbackReviewQueueClient(port).read(credential, null));
        assertEquals(LoopbackReviewQueueClient.Failure.UNAVAILABLE, error.failure());
        assertFalse(error.getMessage().contains(credential));
    }
}
