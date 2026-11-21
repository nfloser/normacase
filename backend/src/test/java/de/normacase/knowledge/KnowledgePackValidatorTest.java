package de.normacase.knowledge;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import de.normacase.common.InvalidDocumentException;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;

class KnowledgePackValidatorTest {

    static final Path KNOWLEDGE = Path.of("../knowledge");

    static Stream<Path> packs() throws IOException {
        return Files.walk(KNOWLEDGE).filter(p -> p.getFileName().toString().equals("pack.json")).sorted();
    }

    @ParameterizedTest(name = "{0}")
    @MethodSource("packs")
    void everyBundledPackIsValid(Path pack) throws IOException {
        KnowledgePack read = new KnowledgePackReader().read(Files.readString(pack));
        assertThat(read.manifest().releaseId()).isNotBlank();
    }

    @Test
    void rejectsUnknownProperties() {
        assertThatThrownBy(() -> new KnowledgePackReader().read(minimal().replace("\"fields\"", "\"extra\": 1, \"fields\"")))
                .isInstanceOf(InvalidDocumentException.class).hasMessageContaining("$.extra");
    }

    @Test
    void rejectsDuplicateProperties() {
        assertThatThrownBy(() -> new KnowledgePackReader().read(minimal().replace("\"fields\"", "\"fields\": [], \"fields\"")))
                .isInstanceOf(InvalidDocumentException.class);
    }

    @Test
    void acceptsTheMinimalPack() {
        assertThat(new KnowledgePackReader().read(minimal()).rules()).hasSize(1);
    }

    @Test
    void reportsStableErrorCodes() {
        String broken = minimal()
                .replace("\"onUnknown\": \"INCOMPLETE\"", "\"onUnknown\": \"SUPPORTED\"")
                .replace("\"field\": \"a\"", "\"field\": \"fehlt\"")
                .replace("\"sourceId\": \"S\"", "\"sourceId\": \"X\"");

        assertThatThrownBy(() -> new KnowledgePackReader().read(broken))
                .isInstanceOfSatisfying(KnowledgeValidationException.class, e -> assertThat(codes(e))
                        .contains("unsafe_on_unknown", "missing_field", "missing_source"));
    }

    @Test
    void rejectsOverlappingRuleVersions() {
        String pack = minimal().replace("\"rules\": [", "\"rules\": [" + rule(2, "2026-06-01") + ",");
        assertThatThrownBy(() -> new KnowledgePackReader().read(pack))
                .isInstanceOfSatisfying(KnowledgeValidationException.class,
                        e -> assertThat(codes(e)).contains("overlapping_rule_validity"));
    }

    @Test
    void publicReferenceNeedsProvenance() {
        String pack = minimal().replace("\"SYNTHETIC\"", "\"PUBLIC_REFERENCE\"");
        assertThatThrownBy(() -> new KnowledgePackReader().read(pack))
                .isInstanceOfSatisfying(KnowledgeValidationException.class, e -> assertThat(codes(e))
                        .contains("missing_source_location", "missing_source_retrieved_at", "missing_source_content_hash"));
    }

    private static List<String> codes(KnowledgeValidationException e) {
        return e.errors().stream().map(KnowledgeValidationException.Error::code).toList();
    }

    static String rule(int version, String from) {
        return """
                {"id": "R", "version": %d, "validFrom": "%s", "validUntil": null, "sourceId": "S",
                 "condition": {"kind": "field_equals", "field": "a", "expected": "YES"},
                 "onMatch": "SUPPORTED", "onNoMatch": "NOT_SUPPORTED", "onUnknown": "INCOMPLETE"}""".formatted(version, from);
    }

    static String minimal() {
        return """
                {"manifest": {"formatVersion": 1, "packId": "p", "releaseId": "r-1", "lifecycleStatus": "ACTIVE",
                  "validationLevel": "SYNTHETIC", "entryRuleId": "R"},
                 "fields": [{"id": "a", "type": "truth", "required": true}],
                 "evidenceRequirements": [],
                 "sources": [{"id": "S", "authority": "Test", "title": "T", "documentType": "SYNTHETIC", "status": "ACTIVE", "version": "1"}],
                 "rules": [%s]}""".formatted(rule(1, "2026-01-01"));
    }
}
