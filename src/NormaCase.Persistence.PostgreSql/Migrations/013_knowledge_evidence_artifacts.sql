CREATE TABLE normacase.knowledge_evidence_artifacts (
    evidence_id text PRIMARY KEY CHECK (length(evidence_id) BETWEEN 1 AND 256),
    kind text NOT NULL CHECK (kind IN ('SOURCE','IMPACT','TESTS')),
    title text NOT NULL CHECK (length(title) BETWEEN 1 AND 256),
    content text NOT NULL CHECK (octet_length(content) BETWEEN 1 AND 65536),
    content_sha256 text NOT NULL CHECK (content_sha256 ~ '^[0-9a-f]{64}$'),
    recorded_by_actor_id text NOT NULL CHECK (length(recorded_by_actor_id) BETWEEN 1 AND 256),
    recorded_at_utc timestamptz NOT NULL
);
CREATE TRIGGER knowledge_evidence_artifacts_no_mutation
BEFORE UPDATE OR DELETE ON normacase.knowledge_evidence_artifacts
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
