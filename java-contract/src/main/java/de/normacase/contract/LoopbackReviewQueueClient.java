package de.normacase.contract;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Proxy;
import java.net.ProxySelector;
import java.net.SocketAddress;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.Base64;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.Flow;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/** Synthetic development client: .NET remains responsible for every access decision. */
public final class LoopbackReviewQueueClient {
    private static final int MAX_RESPONSE = 1_048_576;
    private final URI endpoint;
    private final HttpClient client;
    private final ReviewQueueParser parser = new ReviewQueueParser();

    public LoopbackReviewQueueClient(int port) {
        if (port < 1 || port > 65535) throw new IllegalArgumentException("Invalid local port");
        endpoint = URI.create("http://127.0.0.1:" + port + "/api/review/work-queues");
        ProxySelector noProxy = new ProxySelector() {
            @Override public List<Proxy> select(URI uri) { return List.of(Proxy.NO_PROXY); }
            @Override public void connectFailed(URI uri, SocketAddress address, IOException failure) { }
        };
        client = HttpClient.newBuilder().proxy(noProxy).followRedirects(HttpClient.Redirect.NEVER)
            .connectTimeout(Duration.ofSeconds(2)).build();
    }

    public ReviewQueuePage read(String credential, String cursor) throws QueueReadException {
        return parser.parse(readValidatedJson(credential, cursor));
    }

    /** Return the validated original JSON to preserve future fields and exact revisions. */
    public String readValidatedJson(String credential, String cursor) throws QueueReadException {
        if (!validCredential(credential) || (cursor != null && (cursor.isEmpty() || cursor.length() > 256)))
            throw new QueueReadException(Failure.INVALID_REQUEST);
        String query = cursor == null ? "" : "?afterCaseId=" + URLEncoder.encode(cursor, StandardCharsets.UTF_8);
        var request = HttpRequest.newBuilder(URI.create(endpoint + query)).GET().timeout(Duration.ofSeconds(5))
            .header("Authorization", "Bearer " + credential).header("Accept", "application/json")
            .header("Cache-Control", "no-store").build();
        var pending = client.sendAsync(request, ignored -> new LimitedBody());
        try {
            var response = pending.get(5, TimeUnit.SECONDS);
            if (response.statusCode() == 400) throw new QueueReadException(Failure.INVALID_REQUEST);
            if (response.statusCode() == 401) throw new QueueReadException(Failure.UNAUTHORIZED);
            if (response.statusCode() == 403) throw new QueueReadException(Failure.FORBIDDEN);
            if (response.statusCode() != 200) throw new QueueReadException(Failure.UPSTREAM_FAILURE);
            String media = response.headers().firstValue("Content-Type").orElse("").split(";", 2)[0].trim();
            if (!media.equalsIgnoreCase("application/json")) throw new QueueReadException(Failure.INVALID_RESPONSE);
            String text = StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(response.body())).toString();
            try { parser.parse(text); return text; }
            catch (IllegalArgumentException ex) { throw new QueueReadException(Failure.INVALID_RESPONSE); }
        } catch (InterruptedException ex) {
            pending.cancel(true);
            Thread.currentThread().interrupt();
            throw new QueueReadException(Failure.UNAVAILABLE);
        } catch (CharacterCodingException ex) {
            throw new QueueReadException(Failure.INVALID_RESPONSE);
        } catch (TimeoutException ex) {
            pending.cancel(true);
            throw new QueueReadException(Failure.UNAVAILABLE);
        } catch (ExecutionException ex) {
            throw new QueueReadException(Failure.UNAVAILABLE);
        }
    }

    private static boolean validCredential(String credential) {
        if (credential == null || credential.length() != 44) return false;
        try {
            byte[] decoded = Base64.getDecoder().decode(credential);
            return decoded.length == 32 && Base64.getEncoder().encodeToString(decoded).equals(credential);
        } catch (IllegalArgumentException ex) { return false; }
    }

    public enum Failure { INVALID_REQUEST, UNAUTHORIZED, FORBIDDEN, UPSTREAM_FAILURE, INVALID_RESPONSE, UNAVAILABLE }
    public static final class QueueReadException extends Exception {
        private final Failure failure;
        private QueueReadException(Failure failure) { super("Synthetic queue request failed"); this.failure = failure; }
        public Failure failure() { return failure; }
    }

    private static final class LimitedBody implements HttpResponse.BodySubscriber<byte[]> {
        private final CompletableFuture<byte[]> result = new CompletableFuture<>();
        private final ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        private Flow.Subscription subscription;
        @Override public CompletionStage<byte[]> getBody() { return result; }
        @Override public void onSubscribe(Flow.Subscription s) { subscription = s; s.request(1); }
        @Override public void onNext(List<ByteBuffer> chunks) {
            for (ByteBuffer chunk : chunks) {
                if (chunk.remaining() > MAX_RESPONSE - bytes.size()) {
                    subscription.cancel(); result.completeExceptionally(new IOException("Response too large")); return;
                }
                byte[] copy = new byte[chunk.remaining()]; chunk.get(copy); bytes.writeBytes(copy);
            }
            subscription.request(1);
        }
        @Override public void onError(Throwable error) { result.completeExceptionally(new IOException("Response unavailable")); }
        @Override public void onComplete() { result.complete(bytes.toByteArray()); }
    }
}
