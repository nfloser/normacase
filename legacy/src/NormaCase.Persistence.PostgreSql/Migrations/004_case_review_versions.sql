CREATE TABLE normacase.case_review_versions (
    case_id text NOT NULL CHECK (length(btrim(case_id)) > 0),
    version bigint NOT NULL CHECK (version >= 0),
    assessment_id text NOT NULL REFERENCES normacase.assessment_records(assessment_id),
    state_json json NOT NULL,
    state_sha256 text NOT NULL CHECK (state_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (case_id, version)
);
CREATE TRIGGER case_review_versions_no_update
BEFORE UPDATE ON normacase.case_review_versions FOR EACH ROW
EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER case_review_versions_no_delete
BEFORE DELETE ON normacase.case_review_versions FOR EACH ROW
EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
