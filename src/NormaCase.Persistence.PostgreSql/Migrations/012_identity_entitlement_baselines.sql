CREATE TABLE normacase.identity_entitlement_baselines (
    actor_id text PRIMARY KEY CHECK (actor_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]{0,127}$'),
    actions text[] NOT NULL CHECK (cardinality(actions) <= 32),
    case_ids text[] NOT NULL CHECK (cardinality(case_ids) <= 500)
);

CREATE TRIGGER identity_entitlement_baselines_no_mutation
BEFORE UPDATE OR DELETE ON normacase.identity_entitlement_baselines
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
