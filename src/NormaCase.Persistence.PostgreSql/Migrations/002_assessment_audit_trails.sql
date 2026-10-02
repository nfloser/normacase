CREATE TABLE normacase.assessment_audit_trail_versions (
    assessment_id text NOT NULL
        REFERENCES normacase.assessment_records(assessment_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    last_sequence bigint NOT NULL
        CHECK (last_sequence > 0),
    occurred_at_utc timestamptz NOT NULL,
    occurred_at_utc_ticks bigint NOT NULL,
    audit_format_version integer NOT NULL,
    audit_json json NOT NULL,
    audit_sha256 text NOT NULL
        CHECK (audit_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (assessment_id, last_sequence)
);

CREATE FUNCTION normacase.reject_audit_trail_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'assessment audit trail versions are append-only'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER assessment_audit_trails_no_update
BEFORE UPDATE ON normacase.assessment_audit_trail_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_audit_trail_mutation();

CREATE TRIGGER assessment_audit_trails_no_delete
BEFORE DELETE ON normacase.assessment_audit_trail_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
