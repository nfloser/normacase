package de.normacase.contract;

import static org.junit.jupiter.api.Assertions.*;
import org.junit.jupiter.api.Test;

class ReviewCaseDetailParserTest {
    private final ReviewCaseDetailParser parser = new ReviewCaseDetailParser();
    private static final String DETAIL = """
        {"caseId":"demo-g-review","caseRevision":"90071992547409931234",
         "processRevision":"2","stateId":"future-state","assessmentId":"assessment-demo",
         "packId":"synthetic","assessmentJson":"{\\"value\\":1.2300,\\"result\\":\\"UNKNOWN\\"}",
         "evidence":{"finding":"UNKNOWN"},"allowedActions":[],"auditRevision":"3",
         "audit":[{"sequence":"3","kind":"ASSESSMENT_CREATED","occurredAtUtc":"2026-01-01T00:00:00Z",
                   "actorId":"synthetic","disposition":null,"reason":null,"overrideOutcome":null}],
         "future":{"precision":1.2300}}
        """;

    @Test void validatesWithoutChangingTheOriginalResponse() {
        assertEquals(DETAIL, parser.validate(DETAIL, "demo-g-review"));
    }

    @Test void rejectsMalformedAmbiguousOrMismatchedResponsesWithoutEchoingData() {
        for (String bad : new String[] {
            DETAIL.replace("\"caseRevision\":\"90071992547409931234\"", "\"caseRevision\":90071992547409931234"),
            DETAIL.replace("\"assessmentJson\":\"", "\"assessmentJson\":null,\"other\":\""),
            DETAIL.replace("\"finding\":\"UNKNOWN\"", "\"finding\":null"),
            DETAIL.replace("\"sequence\":\"3\"", "\"sequence\":3"),
            DETAIL.replace("\"caseId\":\"demo-g-review\"", "\"caseId\":\"demo-g-review\",\"caseId\":\"other\""),
            DETAIL + " {}", "{private-data" }) {
            var ex = assertThrows(IllegalArgumentException.class, () -> parser.validate(bad, "demo-g-review"));
            assertEquals("Invalid review case response", ex.getMessage());
        }
        assertThrows(IllegalArgumentException.class, () -> parser.validate(DETAIL, "other"));
        assertThrows(IllegalArgumentException.class, () -> parser.validate("x".repeat(1_048_577), "demo-g-review"));
    }
}
