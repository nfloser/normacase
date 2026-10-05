CREATE TABLE normacase.knowledge_release_imports (
    pack_id text NOT NULL,
    release_id text NOT NULL,
    actor_id text NOT NULL CHECK (length(actor_id) BETWEEN 1 AND 256),
    imported_at_utc timestamptz NOT NULL,
    pack_sha256 text NOT NULL CHECK (pack_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (pack_id,release_id),
    FOREIGN KEY (pack_id,release_id) REFERENCES normacase.knowledge_release_artifacts(pack_id,release_id)
);
CREATE TRIGGER knowledge_release_imports_no_mutation
BEFORE UPDATE OR DELETE ON normacase.knowledge_release_imports
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
