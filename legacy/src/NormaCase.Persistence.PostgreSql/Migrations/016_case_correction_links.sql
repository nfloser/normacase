CREATE TABLE normacase.case_correction_links (
    correction_id text PRIMARY KEY CHECK (length(correction_id) BETWEEN 1 AND 128),
    case_id text NOT NULL,
    review_version bigint NOT NULL CHECK (review_version > 0),
    previous_assessment_id text NOT NULL REFERENCES normacase.assessment_records(assessment_id),
    assessment_id text NOT NULL UNIQUE REFERENCES normacase.assessment_records(assessment_id),
    link_json json NOT NULL,
    link_sha256 text NOT NULL CHECK (link_sha256 ~ '^[0-9a-f]{64}$'),
    UNIQUE (case_id,review_version),
    FOREIGN KEY (case_id,review_version) REFERENCES normacase.case_review_versions(case_id,version)
);
CREATE TRIGGER case_correction_links_no_mutation
BEFORE UPDATE OR DELETE ON normacase.case_correction_links
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
