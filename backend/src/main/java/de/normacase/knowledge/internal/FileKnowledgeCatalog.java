package de.normacase.knowledge.internal;

import de.normacase.knowledge.KnowledgeCatalog;
import de.normacase.knowledge.KnowledgePack;
import de.normacase.knowledge.KnowledgePackReader;
import de.normacase.knowledge.KnowledgeRelease;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Comparator;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.TreeMap;
import java.util.stream.Stream;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;

/**
 * Lädt beim Start alle {@code pack.json} aus dem Knowledge-Verzeichnis. Releases sind unveränderlich: Taucht eine
 * bekannte Release-ID mit anderem Inhalt auf, startet die Anwendung nicht. Unveränderliche Releases werden im
 * Speicher gehalten (Caching ohne Invalidierung).
 */
@Component
class FileKnowledgeCatalog implements KnowledgeCatalog {

    private static final Logger log = LoggerFactory.getLogger(FileKnowledgeCatalog.class);

    private final Map<String, KnowledgeRelease> releases;
    private final Map<String, Map<String, String>> examples = new java.util.HashMap<>();

    FileKnowledgeCatalog(@Value("${normacase.knowledge.directory}") Path directory, JdbcClient jdbc) {
        this.releases = load(directory, examples);
        register(jdbc);
        log.info("Knowledge Releases geladen: {}", releases.keySet());
    }

    @Override
    public Optional<KnowledgeRelease> release(String releaseId) {
        return Optional.ofNullable(releases.get(releaseId));
    }

    @Override
    public List<KnowledgeRelease> releases() {
        return List.copyOf(releases.values());
    }

    @Override
    public Optional<String> example(String releaseId, String exampleId) {
        return Optional.ofNullable(examples.getOrDefault(releaseId, Map.of()).get(exampleId));
    }

    private static Map<String, KnowledgeRelease> load(Path directory, Map<String, Map<String, String>> examples) {
        if (!Files.isDirectory(directory)) {
            throw new IllegalStateException("Knowledge-Verzeichnis fehlt: " + directory.toAbsolutePath());
        }
        KnowledgePackReader reader = new KnowledgePackReader();
        Map<String, KnowledgeRelease> result = new TreeMap<>();
        try (Stream<Path> files = Files.walk(directory)) {
            for (Path file : files.filter(p -> p.getFileName().toString().equals("pack.json"))
                    .sorted(Comparator.naturalOrder()).toList()) {
                byte[] bytes = Files.readAllBytes(file);
                KnowledgePack pack = reader.read(new String(bytes, StandardCharsets.UTF_8));
                Path presentation = file.resolveSibling("presentation.de-DE.json");
                String labels = null;
                if (Files.isRegularFile(presentation)) {
                    labels = Files.readString(presentation, StandardCharsets.UTF_8);
                    de.normacase.common.StrictJson.parseObject(labels);
                }
                KnowledgeRelease release = new KnowledgeRelease(pack, sha256(bytes), labels);
                if (result.putIfAbsent(release.releaseId(), release) != null) {
                    throw new IllegalStateException("Release-ID doppelt vorhanden: " + release.releaseId());
                }
                if (labels != null) {
                    examples.put(release.releaseId(), readExamples(file.resolveSibling("examples"), labels));
                }
            }
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
        return Map.copyOf(result);
    }

    /** Beispiele aus {@code examples/} neben dem Pack; nur einfache Dateinamen, jedes Beispiel muss vorhanden sein. */
    private static Map<String, String> readExamples(Path folder, String presentationJson) throws IOException {
        Map<String, String> found = new TreeMap<>();
        var listed = de.normacase.common.StrictJson.parseObject(presentationJson).raw("examples");
        if (listed == null || !listed.isArray()) {
            return Map.of();
        }
        for (var entry : listed) {
            String id = entry.path("id").isString() ? entry.path("id").stringValue() : "";
            String file = entry.path("caseFile").isString() ? entry.path("caseFile").stringValue() : "";
            if (id.isEmpty() || !file.matches("[A-Za-z0-9._-]+\\.json")) {
                throw new IllegalStateException("Ungültiges Beispiel in " + folder.getParent() + ": " + id);
            }
            Path path = folder.resolve(file);
            if (!Files.isRegularFile(path)) {
                throw new IllegalStateException("Beispieldatei fehlt: " + path);
            }
            String json = Files.readString(path, StandardCharsets.UTF_8);
            de.normacase.common.StrictJson.parseObject(json);
            found.put(id, json);
        }
        return Map.copyOf(found);
    }

    private void register(JdbcClient jdbc) {
        for (KnowledgeRelease release : releases.values()) {
            Optional<String> known = jdbc.sql("select content_sha256 from knowledge.release where release_id = :id")
                    .param("id", release.releaseId()).query(String.class).optional();
            if (known.isEmpty()) {
                var m = release.pack().manifest();
                jdbc.sql("""
                        insert into knowledge.release (release_id, pack_id, content_sha256, validation_level, lifecycle_status)
                        values (:id, :pack, :hash, :level, :status) on conflict (release_id) do nothing""")
                        .param("id", release.releaseId()).param("pack", m.packId()).param("hash", release.contentSha256())
                        .param("level", m.validationLevel()).param("status", m.lifecycleStatus()).update();
            } else if (!known.get().equals(release.contentSha256())) {
                throw new IllegalStateException("Release " + release.releaseId()
                        + " wurde verändert. Historische Releases sind unveränderlich; bitte neue Release-ID vergeben.");
            }
        }
    }

    static String sha256(byte[] bytes) {
        try {
            return "sha256:" + HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(bytes));
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }
}
