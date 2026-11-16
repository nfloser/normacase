package de.normacase.http;

import de.normacase.contract.LoopbackReviewQueueClient;
import jakarta.servlet.http.HttpServletRequest;
import java.util.Base64;
import java.util.Collections;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public final class QueueEndpoint {
    @FunctionalInterface interface QueueSource { String read(String credential, String cursor, Integer pageSize) throws LoopbackReviewQueueClient.QueueReadException; }
    private final QueueSource source;
    public QueueEndpoint() { this(new LoopbackReviewQueueClient(5080)::readValidatedJson); }
    QueueEndpoint(QueueSource source) { this.source = source; }

    @GetMapping(value = "/api/review/work-queues", produces = MediaType.APPLICATION_JSON_VALUE)
    public ResponseEntity<String> queue(HttpServletRequest request) {
        var headers = Collections.list(request.getHeaders("Authorization"));
        if (headers.size() != 1 || !headers.getFirst().startsWith("Bearer ") || !validCredential(headers.getFirst().substring(7)))
            return unauthorized();
        var parameters = request.getParameterMap();
        if (parameters.keySet().stream().anyMatch(name -> !name.equals("afterCaseId") && !name.equals("pageSize"))) return error(400);
        String[] values = parameters.get("afterCaseId");
        if (values != null && (values.length != 1 || values[0].isEmpty() || values[0].length() > 256)) return error(400);
        String[] sizes = parameters.get("pageSize");
        if (sizes != null && (sizes.length != 1 || !sizes[0].matches("[0-9]{1,3}"))) return error(400);
        Integer size = sizes == null ? null : Integer.valueOf(sizes[0]);
        if (size != null && (size < 1 || size > 100)) return error(400);
        try {
            String json = source.read(headers.getFirst().substring(7), values == null ? null : values[0], size);
            return ResponseEntity.ok().contentType(MediaType.APPLICATION_JSON).body(json);
        } catch (LoopbackReviewQueueClient.QueueReadException ex) {
            return switch (ex.failure()) {
                case UNAUTHORIZED -> unauthorized();
                case FORBIDDEN -> error(403);
                case INVALID_REQUEST -> error(400);
                case UPSTREAM_FAILURE, INVALID_RESPONSE, UNAVAILABLE -> error(502);
            };
        }
    }
    private static boolean validCredential(String credential) {
        if (credential.length() != 44) return false;
        try { byte[] decoded = Base64.getDecoder().decode(credential); return decoded.length == 32 && Base64.getEncoder().encodeToString(decoded).equals(credential); }
        catch (IllegalArgumentException ex) { return false; }
    }
    private static ResponseEntity<String> unauthorized() {
        return ResponseEntity.status(401).header("WWW-Authenticate", "Bearer").contentType(MediaType.APPLICATION_JSON)
            .body(Messages.UNAUTHORIZED);
    }
    private static ResponseEntity<String> error(int status) {
        return ResponseEntity.status(status).contentType(MediaType.APPLICATION_JSON).body(Messages.ERROR);
    }
}
