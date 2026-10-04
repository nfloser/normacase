CREATE TABLE normacase.identity_entitlement_changes (
    change_id text PRIMARY KEY CHECK (change_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    target_actor_id text NOT NULL CHECK (target_actor_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    expected_entitlement_revision bigint NOT NULL CHECK (expected_entitlement_revision >= 0),
    actions text[] NOT NULL CHECK (cardinality(actions) <= 32),
    case_ids text[] NOT NULL CHECK (cardinality(case_ids) <= 500),
    proposer_actor_id text NOT NULL CHECK (proposer_actor_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    proposed_at_utc timestamptz NOT NULL,
    reason text NOT NULL CHECK (length(reason) BETWEEN 1 AND 1000 AND reason !~ '[[:cntrl:]]')
);

CREATE TABLE normacase.identity_entitlement_decisions (
    change_id text PRIMARY KEY REFERENCES normacase.identity_entitlement_changes(change_id),
    decision_actor_id text NOT NULL CHECK (decision_actor_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    decided_at_utc timestamptz NOT NULL,
    approved boolean NOT NULL,
    reason text NOT NULL CHECK (length(reason) BETWEEN 1 AND 1000 AND reason !~ '[[:cntrl:]]')
);

CREATE TABLE normacase.identity_entitlement_versions (
    actor_id text NOT NULL CHECK (actor_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    revision bigint NOT NULL CHECK (revision > 0),
    change_id text NOT NULL UNIQUE REFERENCES normacase.identity_entitlement_changes(change_id),
    actions text[] NOT NULL CHECK (cardinality(actions) <= 32),
    case_ids text[] NOT NULL CHECK (cardinality(case_ids) <= 500),
    PRIMARY KEY (actor_id, revision)
);

CREATE TRIGGER identity_entitlement_changes_no_mutation
BEFORE UPDATE OR DELETE ON normacase.identity_entitlement_changes
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER identity_entitlement_decisions_no_mutation
BEFORE UPDATE OR DELETE ON normacase.identity_entitlement_decisions
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER identity_entitlement_versions_no_mutation
BEFORE UPDATE OR DELETE ON normacase.identity_entitlement_versions
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
