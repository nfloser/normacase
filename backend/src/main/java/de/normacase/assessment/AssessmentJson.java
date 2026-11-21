package de.normacase.assessment;

import de.normacase.assessment.AssessmentResult.ConditionTrace;
import de.normacase.assessment.AssessmentResult.NumericTrace;
import de.normacase.assessment.AssessmentResult.OutputTrace;
import de.normacase.assessment.AssessmentResult.RuleTrace;
import de.normacase.assessment.AssessmentResult.SourceTrace;
import de.normacase.common.InvalidDocumentException;
import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.knowledge.TruthValue;
import java.math.BigDecimal;
import java.time.LocalDate;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.node.ArrayNode;
import tools.jackson.databind.node.ObjectNode;

/**
 * Austauschformate: Falleingabe (Format 1) und Bewertungsdokument (Format 2), kompatibel zum Referenzstand.
 * Unbekannte oder doppelte Eigenschaften werden abgelehnt; Zahlen bleiben exakt.
 */
public final class AssessmentJson {

    public static final int CASE_FORMAT_VERSION = 1;
    public static final int ASSESSMENT_FORMAT_VERSION = 2;

    private AssessmentJson() {
    }

    // ---------------------------------------------------------------- lesen

    public static CaseInput readCase(String json) {
        return readCase(StrictJson.parseObject(json));
    }

    public static CaseInput readCase(Obj root) {
        root.allow(Set.of("formatVersion", "assessmentDate", "facts", "evidence"));
        if (root.integer("formatVersion") != CASE_FORMAT_VERSION) {
            throw new InvalidDocumentException(root.path() + ".formatVersion", "Nicht unterstütztes Eingabeformat");
        }
        LocalDate date = root.date("assessmentDate");
        Obj factsNode = root.object("facts");
        Map<String, CaseValue> facts = new LinkedHashMap<>();
        for (String name : factsNode.names()) {
            facts.put(name, caseValue(factsNode.raw(name), factsNode.path() + "." + name));
        }
        Map<String, EvidenceStatus> evidence = new LinkedHashMap<>();
        Obj evidenceNode = root.optObject("evidence");
        if (evidenceNode != null) {
            for (String name : evidenceNode.names()) {
                JsonNode value = evidenceNode.raw(name);
                String path = evidenceNode.path() + "." + name;
                if (value == null || !value.isString()) {
                    throw new InvalidDocumentException(path, "Belegstatus erwartet");
                }
                try {
                    evidence.put(name, EvidenceStatus.valueOf(value.stringValue()));
                } catch (IllegalArgumentException e) {
                    throw new InvalidDocumentException(path, "Unbekannter Belegstatus");
                }
            }
        }
        return new CaseInput(date, facts, evidence);
    }

    static CaseValue caseValue(JsonNode node, String path) {
        Obj value = Obj.of(node, path);
        String kind = value.string("kind");
        Set<String> names = value.names();
        switch (kind) {
            case "UNKNOWN" -> {
                if (names.size() == 1) {
                    return CaseValue.UNKNOWN;
                }
            }
            case "NUMBER" -> {
                if (names.equals(Set.of("kind", "number")) && value.raw("number").isNumber()) {
                    return CaseValue.number(value.optDecimal("number"));
                }
            }
            case "TRUTH" -> {
                if (names.equals(Set.of("kind", "truth"))) {
                    String truth = value.string("truth");
                    for (TruthValue t : TruthValue.values()) {
                        if (t.name().equals(truth)) {
                            return CaseValue.truth(t);
                        }
                    }
                    throw new InvalidDocumentException(path + ".truth", "Ungültiger Wahrheitswert; unbekannt hat eine eigene Art");
                }
            }
            default -> {
            }
        }
        throw new InvalidDocumentException(path, "Ungültiger oder mehrdeutiger Fallwert");
    }

    // ---------------------------------------------------------------- schreiben

    /** Kanonische Falleingabe: sortierte Schlüssel, exakte Zahlen. Grundlage des Eingabe-Hashes. */
    public static ObjectNode caseInput(CaseInput input) {
        ObjectNode canonical = StrictJson.object();
        canonical.put("formatVersion", CASE_FORMAT_VERSION);
        canonical.put("assessmentDate", input.assessmentDate().toString());
        ObjectNode facts = canonical.putObject("facts");
        input.facts().forEach((k, v) -> facts.set(k, caseValue(v)));
        ObjectNode evidence = canonical.putObject("evidence");
        input.evidence().forEach((k, v) -> evidence.put(k, v.name()));
        return canonical;
    }

    public static ObjectNode document(AssessmentResult result, String platformVersion) {
        ObjectNode root = StrictJson.object();
        root.put("formatVersion", ASSESSMENT_FORMAT_VERSION);
        root.put("platformVersion", platformVersion);
        root.set("assessment", assessment(result));
        return root;
    }

    public static String write(AssessmentResult result, String platformVersion) {
        return StrictJson.write(document(result, platformVersion));
    }

    public static ObjectNode assessment(AssessmentResult result) {
        ObjectNode a = StrictJson.object();
        a.put("knowledgeRelease", result.knowledgeRelease());
        date(a, "assessmentDate", result.assessmentDate());
        a.put("outcome", result.outcome().name());
        ArrayNode missing = a.putArray("missingRequiredFields");
        result.missingRequiredFields().forEach(missing::add);
        if (result.ruleTrace() == null) {
            a.putNull("ruleTrace");
        } else {
            a.set("ruleTrace", rule(result.ruleTrace()));
        }
        ArrayNode outputs = a.putArray("domainOutputs");
        result.domainOutputs().forEach(o -> outputs.add(output(o)));
        return a;
    }

    private static ObjectNode rule(RuleTrace r) {
        ObjectNode n = StrictJson.object();
        n.put("ruleId", r.ruleId());
        n.put("ruleVersion", r.ruleVersion());
        n.put("sourceId", r.sourceId());
        n.put("conditionResult", r.conditionResult().name());
        n.put("outcome", r.outcome().name());
        n.set("condition", condition(r.condition()));
        n.set("source", source(r.source()));
        return n;
    }

    private static ObjectNode output(OutputTrace o) {
        ObjectNode n = StrictJson.object();
        n.put("outputId", o.outputId());
        n.put("outputVersion", o.outputVersion());
        ObjectNode value = n.putObject("value");
        if (o.value() == null) {
            value.put("kind", "UNKNOWN");
        } else {
            value.put("kind", "CHOICE");
            value.put("choice", o.value());
        }
        n.put("conditionResult", o.conditionResult().name());
        n.set("condition", condition(o.condition()));
        n.set("source", source(o.source()));
        return n;
    }

    static ObjectNode condition(ConditionTrace c) {
        ObjectNode n = StrictJson.object();
        n.put("kind", c.kind());
        n.put("result", c.result().name());
        n.put("field", c.field());
        value(n, "expected", c.expected());
        value(n, "actual", c.actual());
        decimal(n, "minimum", c.minimum());
        decimal(n, "maximum", c.maximum());
        ArrayNode children = n.putArray("children");
        c.children().forEach(child -> children.add(condition(child)));
        n.put("evidenceRequirementId", c.evidenceRequirementId());
        if (c.evidenceStatus() == null) {
            n.putNull("evidenceStatus");
        } else {
            n.put("evidenceStatus", c.evidenceStatus().name());
        }
        if (c.numericExpression() == null) {
            n.putNull("numericExpression");
        } else {
            n.set("numericExpression", numeric(c.numericExpression()));
        }
        return n;
    }

    private static ObjectNode numeric(NumericTrace t) {
        ObjectNode n = StrictJson.object();
        n.put("kind", t.kind());
        value(n, "value", t.value());
        n.put("field", t.field());
        decimal(n, "selectedMinimum", t.selectedMinimum());
        decimal(n, "selectedMaximum", t.selectedMaximum());
        decimal(n, "selectedValue", t.selectedValue());
        ArrayNode children = n.putArray("children");
        t.children().forEach(child -> children.add(numeric(child)));
        return n;
    }

    private static ObjectNode source(SourceTrace s) {
        ObjectNode n = StrictJson.object();
        n.put("id", s.id());
        n.put("version", s.version());
        n.put("sourceLocation", s.sourceLocation());
        n.put("authority", s.authority());
        n.put("title", s.title());
        n.put("documentType", s.documentType());
        n.put("status", s.status());
        date(n, "publicationDate", s.publicationDate());
        date(n, "validFrom", s.validFrom());
        date(n, "validUntil", s.validUntil());
        date(n, "retrievedAt", s.retrievedAt());
        n.put("contentHash", s.contentHash());
        return n;
    }

    public static ObjectNode caseValue(CaseValue value) {
        ObjectNode n = StrictJson.object();
        switch (value) {
            case CaseValue.Unknown u -> n.put("kind", "UNKNOWN");
            case CaseValue.Truth t -> {
                n.put("kind", "TRUTH");
                n.put("truth", t.value().name());
            }
            case CaseValue.Number num -> {
                n.put("kind", "NUMBER");
                n.put("number", num.value());
            }
        }
        return n;
    }

    private static void value(ObjectNode parent, String name, CaseValue value) {
        if (value == null) {
            parent.putNull(name);
        } else {
            parent.set(name, caseValue(value));
        }
    }

    private static void decimal(ObjectNode parent, String name, BigDecimal value) {
        if (value == null) {
            parent.putNull(name);
        } else {
            parent.put(name, value);
        }
    }

    private static void date(ObjectNode parent, String name, LocalDate value) {
        if (value == null) {
            parent.putNull(name);
        } else {
            parent.put(name, value.toString());
        }
    }
}
