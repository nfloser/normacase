package de.normacase.http;

import de.normacase.contract.LoopbackReviewQueueClient;
import jakarta.servlet.http.HttpServletRequest;
import java.util.regex.Pattern;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RestController;

/**
 * Read-only pass-through for one synthetic case. The .NET host decides access on every request;
 * unknown and inaccessible cases leave this adapter with the same 404 response.
 */
@RestController
public final class CaseDetailEndpoint {
    static final Pattern CASE_ID = Pattern.compile("[A-Za-z0-9_-]{1,128}");
    @FunctionalInterface interface DetailSource { String read(String credential, String caseId) throws LoopbackReviewQueueClient.QueueReadException; }
    private final DetailSource source;
    public CaseDetailEndpoint() { this(new LoopbackReviewQueueClient(5080)::readCaseDetail); }
    CaseDetailEndpoint(DetailSource source) { this.source = source; }

    @GetMapping(value = "/api/review/work-cases/{caseId}", produces = MediaType.APPLICATION_JSON_VALUE)
    public ResponseEntity<String> detail(@PathVariable("caseId") String caseId, HttpServletRequest request) {
        String credential = BearerRequests.credential(request);
        if (credential == null) return BearerRequests.unauthorized();
        if (request.getQueryString() != null || !request.getParameterMap().isEmpty()) return BearerRequests.error(400);
        if (caseId == null || !CASE_ID.matcher(caseId).matches()) return BearerRequests.error(404);
        try {
            return ResponseEntity.ok().contentType(MediaType.APPLICATION_JSON).body(source.read(credential, caseId));
        } catch (LoopbackReviewQueueClient.QueueReadException ex) {
            return switch (ex.failure()) {
                case UNAUTHORIZED -> BearerRequests.unauthorized();
                case FORBIDDEN -> BearerRequests.error(403);
                case NOT_FOUND, INVALID_REQUEST -> BearerRequests.error(404);
                case UPSTREAM_FAILURE, INVALID_RESPONSE, UNAVAILABLE -> BearerRequests.error(502);
            };
        }
    }
}
