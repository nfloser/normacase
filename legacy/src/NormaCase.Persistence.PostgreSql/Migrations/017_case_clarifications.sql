CREATE TABLE normacase.case_clarification_requests (
    clarification_id text PRIMARY KEY CHECK (length(clarification_id) BETWEEN 1 AND 128),
    case_id text NOT NULL,
    review_version bigint NOT NULL,
    request_json json NOT NULL,
    request_sha256 text NOT NULL CHECK (request_sha256 ~ '^[0-9a-f]{64}$'),
    FOREIGN KEY (case_id,review_version) REFERENCES normacase.case_review_versions(case_id,version)
);
CREATE INDEX case_clarification_request_pages ON normacase.case_clarification_requests(case_id,clarification_id COLLATE "C");
CREATE TABLE normacase.case_clarification_resolutions (
    clarification_id text PRIMARY KEY REFERENCES normacase.case_clarification_requests(clarification_id),
    correction_id text NOT NULL REFERENCES normacase.case_correction_links(correction_id)
);
CREATE TRIGGER case_clarification_requests_no_mutation BEFORE UPDATE OR DELETE ON normacase.case_clarification_requests
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER case_clarification_resolutions_no_mutation BEFORE UPDATE OR DELETE ON normacase.case_clarification_resolutions
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
