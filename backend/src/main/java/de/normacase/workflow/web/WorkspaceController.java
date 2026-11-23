package de.normacase.workflow.web;

import de.normacase.common.InvalidDocumentException;
import de.normacase.common.StrictJson;
import de.normacase.common.StrictJson.Obj;
import de.normacase.identity.Actor;
import de.normacase.identity.Roles;
import de.normacase.workflow.Queue;
import de.normacase.workflow.WorkflowException;
import java.sql.Timestamp;
import java.time.Clock;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.UUID;
import java.util.regex.Pattern;
import org.springframework.http.CacheControl;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.security.core.Authentication;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.node.ObjectNode;

/**
 * Meine Ablage wie im bisherigen Fallexplorer: eigene Ordner mit Unterordnern, Farbmarkierung, Lesezeichen, Notizen
 * und Arbeitslistenfarben. Alles gehört dem angemeldeten Konto; andere Konten sehen davon nichts. Die Ablage ordnet
 * nur – Zustand, Ergebnis und Vier-Augen-Prinzip eines Vorgangs ändert sie nicht.
 */
@RestController
class WorkspaceController {

    static final int MAX_DEPTH = 5;
    static final int MAX_FOLDERS = 200;
    static final int MAX_BATCH = 100;
    private static final Pattern COLOR = Pattern.compile("^#[0-9a-f]{6}$");

    private final JdbcClient jdbc;
    private final Clock clock;

    WorkspaceController(JdbcClient jdbc, Clock clock) {
        this.jdbc = jdbc;
        this.clock = clock;
    }

    @GetMapping(path = "/api/v1/workspace", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> state(Authentication auth) {
        return json(HttpStatus.OK, state(owner(auth)));
    }

    @Transactional
    @PostMapping(path = "/api/v1/workspace/folders", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> createFolder(@RequestBody String body, Authentication auth) {
        String owner = owner(auth);
        Obj request = StrictJson.parseObject(body).allow(Set.of("label", "color", "parentId"));
        String label = label(request);
        String color = color(request.string("color"), "$.color");
        UUID parent = optUuid(request.optString("parentId"), "$.parentId");
        int count = jdbc.sql("select count(*) from workflow.personal_folder where owner = :o").param("o", owner)
                .query(Integer.class).single();
        if (count >= MAX_FOLDERS) {
            throw new InvalidDocumentException("$", "Höchstens " + MAX_FOLDERS + " Ordner");
        }
        if (parent != null && depth(owner, parent) >= MAX_DEPTH) {
            throw new InvalidDocumentException("$.parentId", "Höchstens " + MAX_DEPTH + " Ebenen");
        }
        UUID id = UUID.randomUUID();
        jdbc.sql("""
                insert into workflow.personal_folder (folder_id, owner, parent_id, label, color, created_at)
                values (:id, :o, :p, :l, :c, :at)""")
                .param("id", id).param("o", owner).param("p", parent).param("l", label).param("c", color)
                .param("at", Timestamp.from(clock.instant())).update();
        return json(HttpStatus.CREATED, folder(id, parent, label, color));
    }

    @Transactional
    @PutMapping(path = "/api/v1/workspace/folders/{id}", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> updateFolder(@PathVariable UUID id, @RequestBody String body, Authentication auth) {
        String owner = owner(auth);
        requireFolder(owner, id);
        Obj request = StrictJson.parseObject(body).allow(Set.of("label", "color", "parentId"));
        String label = label(request);
        String color = color(request.string("color"), "$.color");
        UUID parent = optUuid(request.optString("parentId"), "$.parentId");
        if (parent != null) {
            if (ancestors(owner, parent).contains(id)) {
                throw new InvalidDocumentException("$.parentId", "Ein Ordner kann nicht in sich selbst liegen");
            }
            if (depth(owner, parent) + height(owner, id) > MAX_DEPTH) {
                throw new InvalidDocumentException("$.parentId", "Höchstens " + MAX_DEPTH + " Ebenen");
            }
        }
        jdbc.sql("update workflow.personal_folder set label = :l, color = :c, parent_id = :p where folder_id = :id and owner = :o")
                .param("l", label).param("c", color).param("p", parent).param("id", id).param("o", owner).update();
        return json(HttpStatus.OK, folder(id, parent, label, color));
    }

    /** Löscht den Ordner samt Unterordnern; die Vorgänge darin liegen danach wieder ohne eigenen Ordner. */
    @Transactional
    @DeleteMapping("/api/v1/workspace/folders/{id}")
    ResponseEntity<Void> deleteFolder(@PathVariable UUID id, Authentication auth) {
        String owner = owner(auth);
        requireFolder(owner, id);
        jdbc.sql("delete from workflow.personal_folder where folder_id = :id and owner = :o").param("id", id).param("o", owner).update();
        prune(owner);
        return ResponseEntity.noContent().build();
    }

    /**
     * Ordnet bis zu 100 Vorgänge auf einmal: {@code MOVE} (Ordner oder ohne), {@code COLOR} (Farbe oder ohne),
     * {@code BOOKMARK} (an/aus) und {@code NOTE} (Text oder ohne). Antwort ist die vollständige Ablage.
     */
    @Transactional
    @PostMapping(path = "/api/v1/workspace/marks", produces = MediaType.APPLICATION_JSON_VALUE)
    ResponseEntity<String> mark(@RequestBody String body, Authentication auth) {
        String owner = owner(auth);
        Obj request = StrictJson.parseObject(body).allow(Set.of("processIds", "command", "folderId", "color", "bookmark", "note"));
        List<String> raw = request.list("processIds", (n, p) -> {
            if (!n.isString()) {
                throw new InvalidDocumentException(p, "Text erwartet");
            }
            return n.stringValue();
        });
        Set<UUID> ids = new LinkedHashSet<>();
        for (int i = 0; i < raw.size(); i++) {
            ids.add(uuid(raw.get(i), "$.processIds[" + i + "]"));
        }
        if (ids.isEmpty() || ids.size() > MAX_BATCH) {
            throw new InvalidDocumentException("$.processIds", "1 bis " + MAX_BATCH + " Vorgänge");
        }
        for (UUID id : ids) {
            String state = jdbc.sql("select state from workflow.process where process_id = :id").param("id", id)
                    .query(String.class).optional()
                    .orElseThrow(() -> new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Vorgang nicht gefunden"));
            if ("NOTE".equals(request.optString("command")) && "CLOSED".equals(state) && request.has("note")) {
                throw new WorkflowException(WorkflowException.Kind.CONFLICT,
                        "Zu abgeschlossenen Vorgängen werden keine Notizen mehr gespeichert");
            }
        }
        String command = request.string("command");
        switch (command) {
            case "MOVE" -> {
                UUID folder = optUuid(request.optString("folderId"), "$.folderId");
                if (folder != null) {
                    requireFolder(owner, folder);
                }
                upsert(owner, ids, "folder_id", folder);
            }
            case "COLOR" -> upsert(owner, ids, "color",
                    request.has("color") ? color(request.string("color"), "$.color") : null);
            case "BOOKMARK" -> {
                if (!request.has("bookmark")) {
                    throw new InvalidDocumentException("$.bookmark", "Pflichtangabe fehlt");
                }
                upsert(owner, ids, "bookmark", request.bool("bookmark"));
            }
            case "NOTE" -> {
                String note = request.optString("note");
                if (note != null) {
                    note = note.strip();
                    if (note.length() > 1000) {
                        throw new InvalidDocumentException("$.note", "Höchstens 1000 Zeichen");
                    }
                    if (note.isEmpty()) {
                        note = null;
                    }
                }
                upsert(owner, ids, "note", note);
            }
            default -> throw new InvalidDocumentException("$.command", "Unbekannter Befehl");
        }
        prune(owner);
        return json(HttpStatus.OK, state(owner));
    }

    /** Eigene Farbe einer Arbeitsliste; ohne Farbe gilt wieder die Vorgabe. */
    @Transactional
    @PutMapping("/api/v1/workspace/queue-colors/{queue}")
    ResponseEntity<Void> queueColor(@PathVariable String queue, @RequestBody String body, Authentication auth) {
        String owner = owner(auth);
        try {
            Queue.fromId(queue);
        } catch (IllegalArgumentException e) {
            throw new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Unbekannte Arbeitsliste");
        }
        Obj request = StrictJson.parseObject(body).allow(Set.of("color"));
        jdbc.sql("delete from workflow.personal_queue_color where owner = :o and queue = :q").param("o", owner).param("q", queue).update();
        if (request.has("color")) {
            jdbc.sql("insert into workflow.personal_queue_color (owner, queue, color) values (:o, :q, :c)")
                    .param("o", owner).param("q", queue).param("c", color(request.string("color"), "$.color")).update();
        }
        return ResponseEntity.noContent().build();
    }

    private ObjectNode state(String owner) {
        ObjectNode o = StrictJson.object();
        var folders = o.putArray("folders");
        jdbc.sql("select folder_id, parent_id, label, color from workflow.personal_folder where owner = :o order by lower(label), folder_id")
                .param("o", owner)
                .query((rs, n) -> folders.add(folder(rs.getObject(1, UUID.class), rs.getObject(2, UUID.class), rs.getString(3), rs.getString(4))))
                .list();
        var marks = o.putArray("marks");
        jdbc.sql("select process_id, folder_id, color, bookmark, note from workflow.personal_mark where owner = :o order by process_id")
                .param("o", owner)
                .query((rs, n) -> {
                    ObjectNode m = marks.addObject();
                    m.put("processId", rs.getString(1));
                    m.put("folderId", rs.getString(2));
                    m.put("color", rs.getString(3));
                    m.put("bookmark", rs.getBoolean(4));
                    m.put("note", rs.getString(5));
                    return m;
                }).list();
        ObjectNode colors = o.putObject("queueColors");
        jdbc.sql("select queue, color from workflow.personal_queue_color where owner = :o order by queue").param("o", owner)
                .query((rs, n) -> colors.put(rs.getString(1), rs.getString(2))).list();
        return o;
    }

    private void upsert(String owner, Set<UUID> ids, String column, Object value) {
        for (UUID id : ids) {
            jdbc.sql("insert into workflow.personal_mark (process_id, owner, " + column + ") values (:id, :o, :v) "
                            + "on conflict (process_id, owner) do update set " + column + " = excluded." + column)
                    .param("id", id).param("o", owner).param("v", value).update();
        }
    }

    /** Leere Markierungen nicht aufheben. */
    private void prune(String owner) {
        jdbc.sql("""
                delete from workflow.personal_mark where owner = :o and folder_id is null and color is null
                and not bookmark and note is null""").param("o", owner).update();
    }

    private void requireFolder(String owner, UUID id) {
        boolean exists = jdbc.sql("select count(*) from workflow.personal_folder where folder_id = :id and owner = :o")
                .param("id", id).param("o", owner).query(Integer.class).single() > 0;
        if (!exists) {
            throw new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Ordner nicht gefunden");
        }
    }

    /** Ordner vom gegebenen bis zur obersten Ebene; zugleich Prüfung, dass er dem Konto gehört. */
    private List<UUID> ancestors(String owner, UUID folder) {
        List<UUID> chain = new ArrayList<>();
        UUID current = folder;
        while (current != null && chain.size() <= MAX_DEPTH + 1) {
            requireFolder(owner, current);
            chain.add(current);
            current = jdbc.sql("select parent_id from workflow.personal_folder where folder_id = :id").param("id", current)
                    .query((rs, n) -> rs.getObject(1, UUID.class)).single();
        }
        return chain;
    }

    private int depth(String owner, UUID folder) {
        return ancestors(owner, folder).size();
    }

    /** Ebenen unterhalb und einschließlich des Ordners. */
    private int height(String owner, UUID folder) {
        List<UUID> children = jdbc.sql("select folder_id from workflow.personal_folder where parent_id = :id and owner = :o")
                .param("id", folder).param("o", owner).query((rs, n) -> rs.getObject(1, UUID.class)).list();
        int max = 0;
        for (UUID child : children) {
            max = Math.max(max, height(owner, child));
        }
        return max + 1;
    }

    private static ObjectNode folder(UUID id, UUID parent, String label, String color) {
        ObjectNode o = StrictJson.object();
        o.put("id", id.toString());
        o.put("parentId", parent == null ? null : parent.toString());
        o.put("label", label);
        o.put("color", color);
        return o;
    }

    private static String label(Obj request) {
        String label = request.string("label").strip();
        if (label.isEmpty() || label.length() > 80) {
            throw new InvalidDocumentException("$.label", "1 bis 80 Zeichen");
        }
        return label;
    }

    private static String color(String value, String path) {
        String color = value.toLowerCase(Locale.ROOT);
        if (!COLOR.matcher(color).matches()) {
            throw new InvalidDocumentException(path, "Farbe als #rrggbb erwartet");
        }
        return color;
    }

    private static UUID optUuid(String value, String path) {
        return value == null ? null : uuid(value, path);
    }

    private static UUID uuid(String value, String path) {
        try {
            return UUID.fromString(value);
        } catch (IllegalArgumentException e) {
            throw new InvalidDocumentException(path, "Kennung erwartet");
        }
    }

    private static String owner(Authentication auth) {
        boolean reader = auth.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_" + Roles.ASSESSOR)
                || a.getAuthority().equals("ROLE_" + Roles.APPROVER));
        if (!reader) {
            throw new WorkflowException(WorkflowException.Kind.NOT_FOUND, "Nicht gefunden");
        }
        return Actor.from(auth).id();
    }

    private static ResponseEntity<String> json(HttpStatus status, JsonNode node) {
        return ResponseEntity.status(status).contentType(MediaType.APPLICATION_JSON).cacheControl(CacheControl.noStore())
                .body(StrictJson.write(node));
    }
}
