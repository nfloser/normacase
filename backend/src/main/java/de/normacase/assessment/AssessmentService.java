package de.normacase.assessment;

import de.normacase.assessment.AssessmentResult.ConditionTrace;
import de.normacase.common.StrictJson;
import de.normacase.identity.Actor;
import de.normacase.knowledge.KnowledgeCatalog;
import de.normacase.knowledge.KnowledgeRelease;
import de.normacase.system.PlatformVersion;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Clock;
import java.util.ArrayList;
import java.util.HexFormat;
import java.util.List;
import java.util.UUID;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.node.ObjectNode;

/**
 * Anwendungsfall „Fall bewerten“: Regelwerk zum Release holen, deterministisch bewerten, Nachweis auslösen.
 * Der Fallinhalt verlässt diese Methode nur im Ergebnis an den Aufrufer; gespeichert wird er nicht.
 */
@Service
public class AssessmentService {

    private final KnowledgeCatalog catalog;
    private final RuleEngine engine = new RuleEngine();
    private final ApplicationEventPublisher events;
    private final PlatformVersion platformVersion;
    private final Clock clock;

    AssessmentService(KnowledgeCatalog catalog, ApplicationEventPublisher events, PlatformVersion platformVersion, Clock clock) {
        this.catalog = catalog;
        this.events = events;
        this.platformVersion = platformVersion;
        this.clock = clock;
    }

    @Transactional
    public Assessment assess(String releaseId, CaseInput input, ExternalReference reference, Actor actor) {
        KnowledgeRelease release = catalog.release(releaseId).orElseThrow(() -> new UnknownReleaseException(releaseId));
        AssessmentResult result = engine.evaluate(release.pack(), input);
        String inputSha256 = inputSha256(input);
        UUID evidenceId = UUID.randomUUID();
        events.publishEvent(new AssessmentCompleted(evidenceId, clock.instant(), reference, inputSha256,
                platformVersion.value(), release.releaseId(), release.contentSha256(), input.assessmentDate(),
                result.ruleTrace() == null ? null : result.ruleTrace().ruleId(),
                result.ruleTrace() == null ? null : result.ruleTrace().ruleVersion(),
                result.outcome(), criteria(result), actor));
        return new Assessment(evidenceId, inputSha256, release.contentSha256(), result,
                AssessmentJson.document(result, platformVersion.value()));
    }

    /** Bewertet erneut, ohne einen Nachweis zu schreiben (Nachvollziehen einer früheren Bewertung). */
    public Assessment replay(String releaseId, CaseInput input) {
        KnowledgeRelease release = catalog.release(releaseId).orElseThrow(() -> new UnknownReleaseException(releaseId));
        AssessmentResult result = engine.evaluate(release.pack(), input);
        return new Assessment(null, inputSha256(input), release.contentSha256(), result,
                AssessmentJson.document(result, platformVersion.value()));
    }

    /** SHA-256 der kanonischen Eingabe: sortierte Schlüssel, exakte Zahlen, kein Leerraum. */
    public static String inputSha256(CaseInput input) {
        ObjectNode canonical = AssessmentJson.caseInput(input);
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(StrictJson.write(canonical).getBytes(StandardCharsets.UTF_8));
            return "sha256:" + HexFormat.of().formatHex(digest);
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }

    static List<AssessmentCompleted.Criterion> criteria(AssessmentResult result) {
        List<AssessmentCompleted.Criterion> list = new ArrayList<>();
        if (result.ruleTrace() != null) {
            flatten(result.ruleTrace().condition(), "rule", list);
        }
        result.domainOutputs().forEach(o -> flatten(o.condition(), "output:" + o.outputId(), list));
        return list;
    }

    private static void flatten(ConditionTrace trace, String path, List<AssessmentCompleted.Criterion> list) {
        list.add(new AssessmentCompleted.Criterion(path, trace.kind(), trace.field(), trace.evidenceRequirementId(),
                trace.result().name(), trace.evidenceStatus() == null ? null : trace.evidenceStatus().name()));
        for (int i = 0; i < trace.children().size(); i++) {
            flatten(trace.children().get(i), path + "." + i, list);
        }
    }

    /** Ergebnis für den Aufrufer: Nachweis-ID, Hashes und das vollständige Bewertungsdokument. */
    public record Assessment(UUID evidenceId, String inputSha256, String knowledgeSha256, AssessmentResult result,
            ObjectNode document) {
    }

}
