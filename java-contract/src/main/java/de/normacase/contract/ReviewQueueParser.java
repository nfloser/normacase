package de.normacase.contract;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.core.JsonParser;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.ArrayList;
import java.util.List;

/** Strict wire-type validation; never logs or embeds case data in errors. */
public final class ReviewQueueParser {
    private final ObjectMapper mapper = new ObjectMapper()
        .enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION)
        .enable(DeserializationFeature.FAIL_ON_TRAILING_TOKENS);

    public ReviewQueuePage parse(String json) {
        if (json == null || json.length() > 1_048_576) throw invalid();
        JsonNode root;
        try { root = mapper.readTree(json); }
        catch (JsonProcessingException ex) { throw invalid(); }
        if (root == null || !root.isObject()) throw invalid();
        JsonNode cursor = root.get("nextPageCursor");
        if (cursor == null || !(cursor.isNull() || cursor.isTextual())) throw invalid();
        JsonNode version = root.get("configurationVersion");
        if (version == null || !version.isIntegralNumber() || !version.canConvertToInt()) throw invalid();
        List<ReviewQueuePage.Queue> queues = new ArrayList<>();
        for (JsonNode queue : array(root, "queues")) {
            List<ReviewQueuePage.Item> items = new ArrayList<>();
            for (JsonNode item : array(queue, "items")) {
                JsonNode batch = item.get("batchAllowed");
                if (batch == null || !batch.isBoolean()) throw invalid();
                items.add(new ReviewQueuePage.Item(text(item, "caseId"), text(item, "caseRevision"),
                    text(item, "processRevision"), text(item, "stateId"), text(item, "assessmentId"),
                    text(item, "auditRevision"), batch.booleanValue()));
            }
            queues.add(new ReviewQueuePage.Queue(text(queue, "queueId"), items));
        }
        return new ReviewQueuePage(text(root, "configurationId"), version.intValue(),
            cursor.isNull() ? null : cursor.textValue(), queues);
    }

    private static JsonNode array(JsonNode parent, String name) {
        if (!parent.isObject()) throw invalid();
        JsonNode value = parent.get(name);
        if (value == null || !value.isArray()) throw invalid();
        return value;
    }
    private static String text(JsonNode parent, String name) {
        if (!parent.isObject()) throw invalid();
        JsonNode value = parent.get(name);
        if (value == null || !value.isTextual()) throw invalid();
        return value.textValue();
    }
    private static IllegalArgumentException invalid() { return new IllegalArgumentException("Invalid review queue response"); }
}
