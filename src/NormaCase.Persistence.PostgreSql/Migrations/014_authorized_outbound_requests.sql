-- A file side effect starts only after its exact authorized command has committed.
CREATE TABLE normacase.authorized_outbound_requests (
    delivery_id text NOT NULL,
    destination_id text NOT NULL,
    actor_id text NOT NULL,
    entitlement_revision bigint NOT NULL CHECK (entitlement_revision >= 0),
    authorized_at_utc timestamptz NOT NULL,
    result_json json NOT NULL,
    result_sha256 text NOT NULL CHECK (result_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (delivery_id, destination_id)
);
CREATE TRIGGER authorized_outbound_requests_immutable
BEFORE UPDATE OR DELETE ON normacase.authorized_outbound_requests
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
