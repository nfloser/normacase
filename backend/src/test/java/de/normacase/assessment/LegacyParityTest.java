package de.normacase.assessment;

import static org.assertj.core.api.Assertions.assertThat;

import de.normacase.common.StrictJson;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePackReader;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.stream.Stream;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import tools.jackson.databind.JsonNode;

/**
 * ADR 0005: Die Java-Engine muss für jeden synthetischen Fall exakt das Bewertungsdokument des eingefrorenen
 * .NET-Referenzstands liefern. Die Erwartungen erzeugt {@code scripts/parity/oracle} im CI-Job {@code parity}.
 */
class LegacyParityTest {

    static final Path REPO = Path.of("..").toAbsolutePath().normalize();
    static final Path GOLDEN = Path.of("src/test/resources/parity");

    static Stream<Arguments> cases() throws IOException {
        JsonNode manifest = StrictJson.MAPPER.readTree(Files.readString(GOLDEN.resolve("cases.json")));
        return java.util.stream.StreamSupport.stream(manifest.spliterator(), false)
                .map(e -> Arguments.of(e.get("name").stringValue(), e.get("pack").stringValue(), e.get("case").stringValue()));
    }

    @ParameterizedTest(name = "{0}")
    @MethodSource("cases")
    void javaEngineReproducesTheReferenceAssessment(String name, String pack, String caseFile) throws IOException {
        KnowledgePack knowledge = new KnowledgePackReader().read(read(REPO.resolve(pack)));
        CaseInput input = AssessmentJson.readCase(read(REPO.resolve(caseFile)));

        String actual = AssessmentJson.write(new RuleEngine().evaluate(knowledge, input), "parity");
        String expected = read(GOLDEN.resolve(name + ".json"));

        assertThat(StrictJson.MAPPER.readTree(actual)).isEqualTo(StrictJson.MAPPER.readTree(expected));
    }

    private static String read(Path path) throws IOException {
        return Files.readString(path, StandardCharsets.UTF_8);
    }
}
