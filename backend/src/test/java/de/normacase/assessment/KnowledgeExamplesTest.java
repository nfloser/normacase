package de.normacase.assessment;

import static org.assertj.core.api.Assertions.assertThat;

import de.normacase.common.StrictJson;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;
import tools.jackson.databind.JsonNode;

/** Die Beispiele der Regelwerke (Prüfwerkstatt, Referenzfälle) liefern genau das dort dokumentierte Ergebnis. */
class KnowledgeExamplesTest {

    static Stream<Arguments> examples() throws IOException {
        List<Arguments> out = new ArrayList<>();
        try (Stream<Path> files = Files.walk(Path.of("../knowledge"))) {
            for (Path presentation : files.filter(p -> p.getFileName().toString().equals("presentation.de-DE.json")).sorted().toList()) {
                JsonNode root = StrictJson.MAPPER.readTree(Files.readString(presentation));
                Path expectedFile = presentation.resolveSibling("examples").resolve("expected-outcomes.json");
                if (!Files.exists(expectedFile)) {
                    continue;
                }
                JsonNode expected = StrictJson.MAPPER.readTree(Files.readString(expectedFile));
                String release = StrictJson.MAPPER.readTree(Files.readString(presentation.resolveSibling("pack.json")))
                        .at("/manifest/releaseId").stringValue();
                for (JsonNode e : root.path("examples")) {
                    out.add(Arguments.of(release + " · " + e.get("id").stringValue(), release,
                            presentation.resolveSibling("examples").resolve(e.get("caseFile").stringValue()),
                            expected.get(e.get("id").stringValue()).stringValue()));
                }
            }
        }
        return out.stream();
    }

    @ParameterizedTest(name = "{0}")
    @MethodSource("examples")
    void exampleHasItsDocumentedOutcome(String name, String release, Path file, String expected) throws IOException {
        var pack = DemoCasesTest.PACKS.get(release);
        var result = new RuleEngine().evaluate(pack, AssessmentJson.readCase(Files.readString(file)));
        assertThat(result.outcome().name()).isEqualTo(expected);
    }
}
