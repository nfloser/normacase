CREATE TABLE normacase.identity_access_audit (
    actor_id text NOT NULL CHECK (
        length(actor_id) BETWEEN 1 AND 128
        AND actor_id !~ '[[:cntrl:]]'
    ),
    revision bigint NOT NULL CHECK (revision > 0),
    suspended boolean NOT NULL,
    administrator_actor_id text NOT NULL CHECK (
        length(administrator_actor_id) BETWEEN 1 AND 128
        AND administrator_actor_id !~ '[[:cntrl:]]'
    ),
    changed_at_utc timestamptz NOT NULL,
    reason text NOT NULL CHECK (
        length(reason) BETWEEN 1 AND 1000
        AND reason !~ '[[:cntrl:]]'
    ),
    PRIMARY KEY (actor_id, revision)
);

CREATE TRIGGER identity_access_audit_no_mutation
BEFORE UPDATE OR DELETE ON normacase.identity_access_audit
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
