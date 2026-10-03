CREATE TABLE normacase.case_review_aggregate_versions (
    case_id text NOT NULL
        CHECK (length(btrim(case_id)) > 0),
    assessment_case_revision bigint NOT NULL
        CHECK (assessment_case_revision > 0),
    process_revision bigint NOT NULL
        CHECK (process_revision >= 0),
    assessment_id text NOT NULL
        REFERENCES normacase.assessment_records(assessment_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    workflow_id text NOT NULL
        CHECK (length(btrim(workflow_id)) > 0),
    workflow_version integer NOT NULL
        CHECK (workflow_version > 0),
    state_id text NOT NULL
        CHECK (length(btrim(state_id)) > 0),
    audit_last_sequence bigint NOT NULL
        CHECK (audit_last_sequence > 0),
    process_format_version integer NOT NULL,
    process_json json NOT NULL,
    process_sha256 text NOT NULL
        CHECK (process_sha256 ~ '^[0-9a-f]{64}$'),
    audit_format_version integer NOT NULL,
    audit_json json NOT NULL,
    audit_sha256 text NOT NULL
        CHECK (audit_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (
        case_id,
        assessment_case_revision,
        process_revision
    ),
    UNIQUE (
        assessment_id,
        audit_last_sequence
    )
);

CREATE INDEX case_review_aggregate_latest_case
ON normacase.case_review_aggregate_versions (
    case_id,
    assessment_case_revision DESC,
    process_revision DESC
);

CREATE FUNCTION normacase.reject_case_review_aggregate_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'case review aggregate versions are append-only'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER case_review_aggregate_no_update
BEFORE UPDATE ON normacase.case_review_aggregate_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_case_review_aggregate_mutation();

CREATE TRIGGER case_review_aggregate_no_delete
BEFORE DELETE ON normacase.case_review_aggregate_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_case_review_aggregate_mutation();
