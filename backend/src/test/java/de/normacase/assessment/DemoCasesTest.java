package de.normacase.assessment;

import static org.assertj.core.api.Assertions.assertThat;

import de.normacase.common.StrictJson;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePackReader;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Map;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;
import tools.jackson.databind.JsonNode;

/** Die fiktiven Vorführfälle (demo/cases) liefern genau das dokumentierte Ergebnis, auch nach der Rückfrage. */
class DemoCasesTest {

    static final Path CASES = Path.of("../demo/cases");
    static final Map<String, KnowledgePack> PACKS = packs();

    static Map<String, KnowledgePack> packs() {
        KnowledgePackReader reader = new KnowledgePackReader();
        try (Stream<Path> files = Files.walk(Path.of("../knowledge"))) {
            return files.filter(p -> p.getFileName().toString().equals("pack.json")).map(p -> {
                try {
                    return reader.read(Files.readString(p));
                } catch (IOException e) {
                    throw new IllegalStateException(e);
                }
            }).collect(Collectors.toMap(p -> p.manifest().releaseId(), p -> p));
        } catch (IOException e) {
            throw new IllegalStateException(e);
        }
    }

    static Stream<Path> cases() throws IOException {
        return Files.list(CASES).filter(p -> Files.exists(p.resolve("case.json"))).sorted();
    }

    @ParameterizedTest(name = "{0}")
    @MethodSource("cases")
    void caseHasItsDocumentedOutcome(Path folder) throws IOException {
        JsonNode meta = StrictJson.MAPPER.readTree(Files.readString(folder.resolve("case.json")));
        KnowledgePack pack = PACKS.get(meta.get("knowledgeRelease").stringValue());
        assertThat(pack).as("Release vorhanden").isNotNull();

        var result = new RuleEngine().evaluate(pack, AssessmentJson.readCase(Files.readString(folder.resolve("input.json"))));
        assertThat(result.outcome().name()).isEqualTo(meta.get("expectedOutcome").stringValue());

        if (Files.exists(folder.resolve("clarification.json"))) {
            var clarified = new RuleEngine().evaluate(pack, AssessmentJson.readCase(Files.readString(folder.resolve("clarification.json"))));
            assertThat(clarified.outcome().name()).isEqualTo(meta.get("expectedOutcomeAfterClarification").stringValue());
        }
        for (JsonNode doc : meta.get("documents")) {
            assertThat(folder.resolve(doc.get("file").stringValue())).exists();
        }
    }
}
