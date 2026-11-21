package de.normacase.common;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.format.DateTimeParseException;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;
import java.util.function.BiFunction;
import tools.jackson.core.JacksonException;
import tools.jackson.core.StreamReadConstraints;
import tools.jackson.core.StreamReadFeature;
import tools.jackson.core.StreamWriteFeature;
import tools.jackson.core.json.JsonFactory;
import tools.jackson.databind.DeserializationFeature;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.cfg.JsonNodeFeature;
import tools.jackson.databind.json.JsonMapper;
import tools.jackson.databind.node.ObjectNode;

/**
 * Striktes Lesen und exaktes Schreiben der Austauschformate: doppelte oder unbekannte Eigenschaften werden
 * abgelehnt, Dezimalzahlen bleiben exakt (inklusive Nachkommastellen), Tiefe und Größe sind begrenzt.
 */
public final class StrictJson {

    public static final int MAX_CHARACTERS = 8 * 1024 * 1024;

    public static final JsonMapper MAPPER = JsonMapper.builder(JsonFactory.builder()
                    .streamReadConstraints(StreamReadConstraints.builder().maxNestingDepth(64).build())
                    .enable(StreamReadFeature.STRICT_DUPLICATE_DETECTION)
                    .enable(StreamWriteFeature.WRITE_BIGDECIMAL_AS_PLAIN)
                    .build())
            .enable(DeserializationFeature.USE_BIG_DECIMAL_FOR_FLOATS)
            .enable(DeserializationFeature.FAIL_ON_TRAILING_TOKENS)
            .disable(JsonNodeFeature.STRIP_TRAILING_BIGDECIMAL_ZEROES)
            .build();

    private StrictJson() {
    }

    public static Obj parseObject(String json) {
        if (json == null || json.isBlank()) {
            throw new InvalidDocumentException("$", "Dokument fehlt");
        }
        if (json.length() > MAX_CHARACTERS) {
            throw new InvalidDocumentException("$", "Dokument zu groß");
        }
        JsonNode root;
        try {
            root = MAPPER.readTree(json);
        } catch (JacksonException e) {
            throw new InvalidDocumentException("$", "Kein gültiges JSON: " + e.getOriginalMessage());
        }
        return Obj.of(root, "$");
    }

    public static String write(JsonNode node) {
        return MAPPER.writeValueAsString(node);
    }

    public static ObjectNode object() {
        return MAPPER.createObjectNode();
    }

    /** Objekt mit Pfadangabe für verständliche Fehlermeldungen. */
    public record Obj(ObjectNode node, String path) {

        public static Obj of(JsonNode node, String path) {
            if (node == null || !node.isObject()) {
                throw new InvalidDocumentException(path, "Objekt erwartet");
            }
            return new Obj((ObjectNode) node, path);
        }

        /** Lehnt Eigenschaften ab, die das Format nicht kennt. */
        public Obj allow(Set<String> names) {
            for (String name : node.propertyNames()) {
                if (!names.contains(name)) {
                    throw new InvalidDocumentException(path + "." + name, "Unbekannte Eigenschaft");
                }
            }
            return this;
        }

        public boolean has(String name) {
            return node.has(name) && !node.get(name).isNull();
        }

        public Set<String> names() {
            Set<String> names = new java.util.LinkedHashSet<>();
            node.propertyNames().forEach(names::add);
            return names;
        }

        public JsonNode raw(String name) {
            return node.get(name);
        }

        public String string(String name) {
            String value = optString(name);
            if (value == null) {
                throw new InvalidDocumentException(path + "." + name, "Pflichtangabe fehlt");
            }
            return value;
        }

        public String optString(String name) {
            JsonNode value = node.get(name);
            if (value == null || value.isNull()) {
                return null;
            }
            if (!value.isString()) {
                throw new InvalidDocumentException(path + "." + name, "Text erwartet");
            }
            return value.stringValue();
        }

        public int integer(String name) {
            JsonNode value = node.get(name);
            if (value == null || !value.isIntegralNumber() || !value.canConvertToInt()) {
                throw new InvalidDocumentException(path + "." + name, "Ganzzahl erwartet");
            }
            return value.intValue();
        }

        public boolean bool(String name) {
            JsonNode value = node.get(name);
            if (value == null || !value.isBoolean()) {
                throw new InvalidDocumentException(path + "." + name, "Wahrheitswert erwartet");
            }
            return value.booleanValue();
        }

        public boolean optBool(String name, boolean fallback) {
            return has(name) ? bool(name) : fallback;
        }

        public BigDecimal optDecimal(String name) {
            JsonNode value = node.get(name);
            if (value == null || value.isNull()) {
                return null;
            }
            if (!value.isNumber()) {
                throw new InvalidDocumentException(path + "." + name, "Zahl erwartet");
            }
            return value.decimalValue();
        }

        public LocalDate date(String name) {
            LocalDate value = optDate(name);
            if (value == null) {
                throw new InvalidDocumentException(path + "." + name, "Datum erwartet");
            }
            return value;
        }

        public LocalDate optDate(String name) {
            String value = optString(name);
            if (value == null) {
                return null;
            }
            try {
                if (value.length() != 10) {
                    throw new DateTimeParseException("length", value, 0);
                }
                return LocalDate.parse(value);
            } catch (DateTimeParseException e) {
                throw new InvalidDocumentException(path + "." + name, "Datum im Format JJJJ-MM-TT erwartet");
            }
        }

        public <T extends Enum<T>> T optEnum(String name, Class<T> type) {
            String value = optString(name);
            if (value == null) {
                return null;
            }
            for (T constant : type.getEnumConstants()) {
                if (constant.name().equals(value)) {
                    return constant;
                }
            }
            throw new InvalidDocumentException(path + "." + name, "Unbekannter Wert");
        }

        public Obj object(String name) {
            return Obj.of(node.get(name), path + "." + name);
        }

        public Obj optObject(String name) {
            return has(name) ? object(name) : null;
        }

        public <T> List<T> list(String name, BiFunction<JsonNode, String, T> mapper) {
            JsonNode value = node.get(name);
            if (value == null || value.isNull()) {
                return List.of();
            }
            if (!value.isArray()) {
                throw new InvalidDocumentException(path + "." + name, "Liste erwartet");
            }
            List<T> result = new ArrayList<>();
            for (int i = 0; i < value.size(); i++) {
                result.add(mapper.apply(value.get(i), path + "." + name + "[" + i + "]"));
            }
            return result;
        }
    }
}
