package de.normacase.contract;

import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

/** Validates the existing read-only wire contract without reconstructing assessment data. */
public final class ReviewCaseDetailParser {
    private final ObjectMapper mapper = new ObjectMapper()
        .enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION)
        .enable(DeserializationFeature.FAIL_ON_TRAILING_TOKENS);

    /** Returns the unchanged original JSON after checking identity and required wire types. */
    public String validate(String json, String expectedCaseId) {
        if (json == null || json.length() > 1_048_576 || expectedCaseId == null || expectedCaseId.isEmpty()) throw invalid();
        JsonNode root;
        try { root = mapper.readTree(json); }
        catch (JsonProcessingException ex) { throw invalid(); }
        if (root == null || !root.isObject() || !expectedCaseId.equals(text(root, "caseId"))) throw invalid();
        for (String field : new String[] {"caseRevision", "processRevision", "stateId", "assessmentId", "packId", "assessmentJson", "auditRevision"}) text(root, field);
        JsonNode evidence = root.get("evidence");
        if (evidence == null || !evidence.isObject()) throw invalid();
        evidence.elements().forEachRemaining(value -> { if (!value.isTextual()) throw invalid(); });
        JsonNode actions = root.get("allowedActions");
        if (actions == null || !actions.isArray()) throw invalid();
        actions.forEach(value -> { if (!value.isTextual()) throw invalid(); });
        JsonNode audit = root.get("audit");
        if (audit == null || !audit.isArray()) throw invalid();
        for (JsonNode event : audit) {
            if (!event.isObject()) throw invalid();
            for (String field : new String[] {"sequence", "kind", "occurredAtUtc", "actorId"}) text(event, field);
            for (String field : new String[] {"disposition", "reason", "overrideOutcome"}) {
                JsonNode value = event.get(field);
                if (value == null || !(value.isNull() || value.isTextual())) throw invalid();
            }
        }
        return json;
    }

    private static String text(JsonNode object, String field) {
        JsonNode value = object.get(field);
        if (value == null || !value.isTextual()) throw invalid();
        return value.textValue();
    }
    private static IllegalArgumentException invalid() { return new IllegalArgumentException("Invalid review case response"); }
}
