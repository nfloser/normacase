package de.normacase.knowledge;

import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.knowledge.KnowledgePack.Condition;
import de.normacase.knowledge.KnowledgePack.EvidenceRequirement;
import de.normacase.knowledge.KnowledgePack.Field;
import de.normacase.knowledge.KnowledgePack.Manifest;
import de.normacase.knowledge.KnowledgePack.NumericExpression;
import de.normacase.knowledge.KnowledgePack.Output;
import de.normacase.knowledge.KnowledgePack.RangeBand;
import de.normacase.knowledge.KnowledgePack.Rule;
import de.normacase.knowledge.KnowledgePack.Source;
import de.normacase.knowledge.KnowledgePack.Workflow;
import de.normacase.knowledge.KnowledgePack.WorkflowState;
import de.normacase.knowledge.KnowledgePack.WorkflowTransition;
import java.util.Set;
import tools.jackson.databind.JsonNode;

/** Liest {@code pack.json} (Format 1) strikt und prüft es fachlich-strukturell. */
public final class KnowledgePackReader {

    private final KnowledgePackValidator validator = new KnowledgePackValidator();

    public KnowledgePack read(String json) {
        KnowledgePack pack = parse(StrictJson.parseObject(json));
        validator.validateOrThrow(pack);
        return pack;
    }

    private static KnowledgePack parse(Obj root) {
        root.allow(Set.of("manifest", "fields", "evidenceRequirements", "sources", "rules", "outputs", "workflows"));
        return new KnowledgePack(
                manifest(root.object("manifest")),
                root.list("fields", (n, p) -> field(Obj.of(n, p))),
                root.list("evidenceRequirements", (n, p) -> new EvidenceRequirement(Obj.of(n, p).allow(Set.of("id")).string("id"))),
                root.list("sources", (n, p) -> source(Obj.of(n, p))),
                root.list("rules", (n, p) -> rule(Obj.of(n, p))),
                root.list("outputs", (n, p) -> output(Obj.of(n, p))),
                root.list("workflows", (n, p) -> workflow(Obj.of(n, p))));
    }

    private static Manifest manifest(Obj o) {
        o.allow(Set.of("formatVersion", "packId", "releaseId", "lifecycleStatus", "validationLevel", "entryRuleId"));
        return new Manifest(o.integer("formatVersion"), o.string("packId"), o.string("releaseId"),
                o.string("lifecycleStatus"), o.string("validationLevel"), o.string("entryRuleId"));
    }

    private static Field field(Obj o) {
        o.allow(Set.of("id", "type", "required"));
        return new Field(o.string("id"), o.string("type"), o.optBool("required", false));
    }

    private static Source source(Obj o) {
        o.allow(Set.of("id", "authority", "title", "documentType", "publicationDate", "validFrom", "validUntil",
                "version", "sourceLocation", "retrievedAt", "contentHash", "status"));
        return new Source(o.string("id"), orEmpty(o, "authority"), orEmpty(o, "title"), orEmpty(o, "documentType"),
                o.optDate("publicationDate"), o.optDate("validFrom"), o.optDate("validUntil"), o.optString("version"),
                o.optString("sourceLocation"), o.optDate("retrievedAt"), o.optString("contentHash"), orEmpty(o, "status"));
    }

    private static Rule rule(Obj o) {
        o.allow(Set.of("id", "version", "validFrom", "validUntil", "sourceId", "condition", "onMatch", "onNoMatch", "onUnknown"));
        return new Rule(o.string("id"), o.integer("version"), o.date("validFrom"), o.optDate("validUntil"),
                o.string("sourceId"), condition(o.object("condition")), o.optEnum("onMatch", AssessmentOutcome.class),
                o.optEnum("onNoMatch", AssessmentOutcome.class), o.optEnum("onUnknown", AssessmentOutcome.class));
    }

    private static Output output(Obj o) {
        o.allow(Set.of("id", "version", "validFrom", "validUntil", "sourceId", "type", "choices", "condition", "onMatch", "onNoMatch",
                "numericExpression"));
        Obj condition = o.optObject("condition");
        Obj expression = o.optObject("numericExpression");
        return new Output(o.string("id"), o.integer("version"), o.date("validFrom"), o.optDate("validUntil"),
                o.string("sourceId"), o.string("type"), o.list("choices", KnowledgePackReader::text),
                condition == null ? null : condition(condition), o.optString("onMatch"), o.optString("onNoMatch"),
                expression == null ? null : numeric(expression));
    }

    private static Condition condition(Obj o) {
        o.allow(Set.of("kind", "field", "evidenceRequirementId", "expected", "threshold", "minimum", "maximum",
                "numericExpression", "conditions"));
        Obj expression = o.optObject("numericExpression");
        return new Condition(o.string("kind"), o.optString("field"), o.optString("evidenceRequirementId"),
                o.optEnum("expected", TruthValue.class), o.optDecimal("threshold"), o.optDecimal("minimum"),
                o.optDecimal("maximum"), expression == null ? null : numeric(expression),
                o.list("conditions", (n, p) -> condition(Obj.of(n, p))));
    }

    private static NumericExpression numeric(Obj o) {
        o.allow(Set.of("kind", "field", "input", "operands", "bands", "id", "factor", "divisor", "decimals"));
        Obj input = o.optObject("input");
        java.math.BigDecimal decimals = o.optDecimal("decimals");
        return new NumericExpression(o.string("kind"), o.optString("field"), input == null ? null : numeric(input),
                o.list("operands", (n, p) -> numeric(Obj.of(n, p))),
                o.list("bands", (n, p) -> {
                    Obj band = Obj.of(n, p).allow(Set.of("minimum", "maximum", "value"));
                    return new RangeBand(band.optDecimal("minimum"), band.optDecimal("maximum"), band.optDecimal("value"));
                }), o.optString("id"), o.optDecimal("factor"), o.optDecimal("divisor"),
                decimals == null ? null : decimals.intValueExact());
    }

    private static Workflow workflow(Obj o) {
        o.allow(Set.of("id", "version", "sourceId", "initialStateId", "states", "transitions"));
        return new Workflow(o.string("id"), o.integer("version"), o.string("sourceId"), o.string("initialStateId"),
                o.list("states", (n, p) -> {
                    Obj s = Obj.of(n, p).allow(Set.of("id", "terminal"));
                    return new WorkflowState(s.string("id"), s.optBool("terminal", false));
                }),
                o.list("transitions", (n, p) -> {
                    Obj t = Obj.of(n, p).allow(Set.of("id", "fromStateId", "toStateId"));
                    return new WorkflowTransition(t.string("id"), t.string("fromStateId"), t.string("toStateId"));
                }));
    }

    private static String orEmpty(Obj o, String name) {
        String value = o.optString(name);
        return value == null ? "" : value;
    }

    private static String text(JsonNode node, String path) {
        if (node == null || !node.isString()) {
            throw new de.normacase.common.InvalidDocumentException(path, "Text erwartet");
        }
        return node.stringValue();
    }
}
