CREATE TABLE normacase.knowledge_changes (
    change_id text PRIMARY KEY CHECK (length(change_id) BETWEEN 1 AND 256),
    pack_id text NOT NULL,
    release_id text NOT NULL,
    pack_sha256 text NOT NULL CHECK (pack_sha256 ~ '^[0-9a-f]{64}$'),
    source_reference text NOT NULL CHECK (length(source_reference) BETWEEN 1 AND 256),
    impact_reference text NOT NULL CHECK (length(impact_reference) BETWEEN 1 AND 256),
    test_reference text NOT NULL CHECK (length(test_reference) BETWEEN 1 AND 256),
    proposer_actor_id text NOT NULL CHECK (length(proposer_actor_id) BETWEEN 1 AND 256),
    proposed_at_utc timestamptz NOT NULL,
    FOREIGN KEY (pack_id, release_id) REFERENCES normacase.knowledge_release_artifacts(pack_id, release_id)
);
CREATE TABLE normacase.knowledge_change_decisions (
    change_id text PRIMARY KEY REFERENCES normacase.knowledge_changes(change_id),
    reviewer_actor_id text NOT NULL CHECK (length(reviewer_actor_id) BETWEEN 1 AND 256),
    reviewed_at_utc timestamptz NOT NULL,
    approved boolean NOT NULL,
    reason text NOT NULL CHECK (length(reason) BETWEEN 1 AND 1000)
);
CREATE TABLE normacase.knowledge_activations (
    pack_id text NOT NULL,
    revision bigint NOT NULL CHECK (revision > 0),
    change_id text NOT NULL REFERENCES normacase.knowledge_change_decisions(change_id),
    actor_id text NOT NULL CHECK (length(actor_id) BETWEEN 1 AND 256),
    activated_at_utc timestamptz NOT NULL,
    PRIMARY KEY (pack_id, revision)
);
CREATE TRIGGER knowledge_changes_no_mutation BEFORE UPDATE OR DELETE ON normacase.knowledge_changes
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER knowledge_change_decisions_no_mutation BEFORE UPDATE OR DELETE ON normacase.knowledge_change_decisions
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER knowledge_activations_no_mutation BEFORE UPDATE OR DELETE ON normacase.knowledge_activations
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
