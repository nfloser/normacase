CREATE TABLE normacase.knowledge_release_artifacts (
    pack_id text NOT NULL CHECK (length(pack_id) BETWEEN 1 AND 256),
    release_id text NOT NULL CHECK (length(release_id) BETWEEN 1 AND 256),
    lifecycle_status text NOT NULL,
    validation_level text NOT NULL,
    pack_json json NOT NULL CHECK (octet_length(pack_json::text) BETWEEN 1 AND 1048576),
    pack_sha256 text NOT NULL CHECK (pack_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (pack_id,release_id)
);
CREATE TRIGGER knowledge_release_artifacts_no_mutation
BEFORE UPDATE OR DELETE ON normacase.knowledge_release_artifacts
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
