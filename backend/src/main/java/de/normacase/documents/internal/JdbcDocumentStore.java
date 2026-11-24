package de.normacase.documents.internal;

import de.normacase.documents.DocumentStore;
import de.normacase.documents.DocumentStore.Reference;
import de.normacase.documents.InvalidDocumentContentException;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashSet;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import java.util.regex.Pattern;
import org.springframework.jdbc.core.simple.JdbcClient;
import org.springframework.stereotype.Component;

@Component
class JdbcDocumentStore implements DocumentStore {

    static final int MAX_DOCUMENT_BYTES = 10 * 1024 * 1024;
    static final int MAX_DOCUMENTS = 20;
    static final int MAX_PAGES = 50;
    private static final Pattern ID = Pattern.compile("[A-Za-z0-9][A-Za-z0-9._-]{0,63}");
    /** Obergrenze je Scan-Seite gegen Dekompressionsbomben (entspricht A4 mit 600 dpi). */
    static final long MAX_PIXELS = 40_000_000L;
    private static final byte[] PNG = {(byte) 0x89, 'P', 'N', 'G', '\r', '\n', 0x1a, '\n'};
    private static final byte[] TIFF_LE = {'I', 'I', 42, 0};
    private static final byte[] TIFF_BE = {'M', 'M', 0, 42};
    private static final Map<String, byte[]> MAGIC = Map.of(
            "application/pdf", "%PDF-".getBytes(StandardCharsets.US_ASCII),
            "image/png", PNG,
            "image/jpeg", new byte[] {(byte) 0xFF, (byte) 0xD8, (byte) 0xFF});
    private static final Set<String> TYPES = Set.of("application/pdf", "image/png", "image/jpeg", "image/tiff", "text/plain");
    private static final Set<String> CATEGORIES = Set.of("ORDER", "MEDICAL", "CARE", "PROOF", "OTHER");
    private static final Pattern FIELD = Pattern.compile("[a-z][a-z0-9_]{0,63}");

    private final JdbcClient jdbc;

    JdbcDocumentStore(JdbcClient jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public void store(UUID processId, List<IncomingDocument> documents) {
        if (documents.size() > MAX_DOCUMENTS) {
            throw new InvalidDocumentContentException("*", "Zu viele Dokumente");
        }
        Set<String> ids = new HashSet<>();
        List<List<byte[]>> previews = new ArrayList<>();
        for (IncomingDocument d : documents) {
            validate(d, ids);
            List<byte[]> pages = d.mediaType().equals("image/tiff") ? TiffPages.render(d.id(), d.content(), MAX_PAGES, MAX_PIXELS) : d.pages();
            int pageCount = pageCount(d, pages);
            for (Reference r : d.references()) {
                if (r == null || r.field() == null || !FIELD.matcher(r.field()).matches() || r.page() < 1 || r.page() > Math.max(1, pageCount)) {
                    throw new InvalidDocumentContentException(d.id(), "Ungültiger Verweis auf Angabe und Seite");
                }
            }
            previews.add(pages);
        }
        for (int k = 0; k < documents.size(); k++) {
            IncomingDocument d = documents.get(k);
            List<byte[]> pages = previews.get(k);
            jdbc.sql("""
                    insert into documents.document (process_id, document_id, title, media_type, sha256, size_bytes, page_count,
                        content, category, issued_on)
                    values (:p, :d, :t, :m, :h, :s, :c, :content, :cat, :issued)""")
                    .param("p", processId).param("d", d.id()).param("t", d.title()).param("m", d.mediaType())
                    .param("h", sha256(d.content())).param("s", d.content().length).param("c", pageCount(d, pages))
                    .param("content", d.content()).param("cat", d.category())
                    .param("issued", d.issuedOn() == null ? null : java.sql.Date.valueOf(d.issuedOn())).update();
            for (int i = 0; i < pages.size(); i++) {
                jdbc.sql("insert into documents.page (process_id, document_id, page, content) values (:p, :d, :n, :c)")
                        .param("p", processId).param("d", d.id()).param("n", i + 1).param("c", pages.get(i)).update();
            }
            for (Reference r : new java.util.LinkedHashSet<>(d.references())) {
                jdbc.sql("insert into documents.reference (process_id, document_id, field_id, page) values (:p, :d, :f, :n)")
                        .param("p", processId).param("d", d.id()).param("f", r.field()).param("n", r.page()).update();
            }
        }
    }

    /** PDF und TIFF: Zahl der Vorschauseiten (bei PDF ohne Vorschau 0); Bilder und Text: eine Seite. */
    private static int pageCount(IncomingDocument d, List<byte[]> pages) {
        return switch (d.mediaType()) {
            case "application/pdf", "image/tiff" -> pages.size();
            default -> 1;
        };
    }

    private static void validate(IncomingDocument d, Set<String> ids) {
        if (d.id() == null || !ID.matcher(d.id()).matches() || !ids.add(d.id())) {
            throw new InvalidDocumentContentException(String.valueOf(d.id()), "Ungültige oder doppelte Dokumentkennung");
        }
        if (d.title() == null || d.title().isBlank() || d.title().length() > 200) {
            throw new InvalidDocumentContentException(d.id(), "Titel fehlt oder ist zu lang");
        }
        if (!TYPES.contains(d.mediaType())) {
            throw new InvalidDocumentContentException(d.id(), "Dokumenttyp nicht erlaubt");
        }
        if (d.content().length == 0 || d.content().length > MAX_DOCUMENT_BYTES) {
            throw new InvalidDocumentContentException(d.id(), "Dokument leer oder zu groß");
        }
        if (!CATEGORIES.contains(d.category())) {
            throw new InvalidDocumentContentException(d.id(), "Unbekannte Dokumentart");
        }
        byte[] magic = MAGIC.get(d.mediaType());
        boolean tiff = d.mediaType().equals("image/tiff");
        if (tiff && !startsWith(d.content(), TIFF_LE) && !startsWith(d.content(), TIFF_BE)) {
            throw new InvalidDocumentContentException(d.id(), "Inhalt passt nicht zum Dokumenttyp");
        }
        if (magic != null && !startsWith(d.content(), magic)) {
            throw new InvalidDocumentContentException(d.id(), "Inhalt passt nicht zum Dokumenttyp");
        }
        if (d.mediaType().equals("text/plain") && !validUtf8(d.content())) {
            throw new InvalidDocumentContentException(d.id(), "Text ist kein gültiges UTF-8");
        }
        if (d.pages().size() > MAX_PAGES || (!d.pages().isEmpty() && !d.mediaType().equals("application/pdf"))) {
            throw new InvalidDocumentContentException(d.id(), "Seitenvorschauen nur für PDF und höchstens " + MAX_PAGES);
        }
        for (byte[] page : d.pages()) {
            if (!startsWith(page, PNG) || page.length > MAX_DOCUMENT_BYTES) {
                throw new InvalidDocumentContentException(d.id(), "Seitenvorschauen müssen PNG sein");
            }
        }
    }

    @Override
    public List<DocumentInfo> list(UUID processId) {
        Map<String, List<Reference>> refs = new java.util.HashMap<>();
        jdbc.sql("select document_id, field_id, page from documents.reference where process_id = :p order by document_id, page, field_id")
                .param("p", processId)
                .query((rs, n) -> refs.computeIfAbsent(rs.getString(1), k -> new ArrayList<>()).add(new Reference(rs.getString(2), rs.getInt(3))))
                .list();
        return jdbc.sql("""
                select document_id, title, media_type, sha256, size_bytes, page_count, category, issued_on from documents.document
                where process_id = :p order by issued_on nulls last, document_id""")
                .param("p", processId)
                .query((rs, n) -> info(rs, refs.getOrDefault(rs.getString(1), List.of())))
                .list();
    }

    @Override
    public Optional<DocumentContent> content(UUID processId, String documentId) {
        return jdbc.sql("""
                select document_id, title, media_type, sha256, size_bytes, page_count, category, issued_on, content
                from documents.document where process_id = :p and document_id = :d""")
                .param("p", processId).param("d", documentId)
                .query((rs, n) -> new DocumentContent(info(rs, List.of()), rs.getBytes(9)))
                .optional();
    }

    @Override
    public Optional<byte[]> page(UUID processId, String documentId, int page) {
        return jdbc.sql("select content from documents.page where process_id = :p and document_id = :d and page = :n")
                .param("p", processId).param("d", documentId).param("n", page)
                .query((rs, n) -> rs.getBytes(1)).optional();
    }

    @Override
    public void deleteAll(UUID processId) {
        jdbc.sql("delete from documents.document where process_id = :p").param("p", processId).update();
    }

    private static DocumentInfo info(java.sql.ResultSet rs, List<Reference> refs) throws java.sql.SQLException {
        java.sql.Date issued = rs.getDate(8);
        return new DocumentInfo(rs.getString(1), rs.getString(2), rs.getString(3), rs.getString(4), rs.getInt(5), rs.getInt(6),
                rs.getString(7), issued == null ? null : issued.toLocalDate(), refs);
    }

    private static boolean startsWith(byte[] content, byte[] prefix) {
        return content.length >= prefix.length && Arrays.equals(content, 0, prefix.length, prefix, 0, prefix.length);
    }

    private static boolean validUtf8(byte[] content) {
        try {
            StandardCharsets.UTF_8.newDecoder().decode(java.nio.ByteBuffer.wrap(content));
            return true;
        } catch (java.nio.charset.CharacterCodingException e) {
            return false;
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
