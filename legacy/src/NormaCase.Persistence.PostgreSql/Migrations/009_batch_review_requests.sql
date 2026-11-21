CREATE TABLE normacase.batch_review_requests (
    request_id text PRIMARY KEY CHECK (
        length(request_id) BETWEEN 1 AND 128
        AND request_id ~ '^[A-Za-z0-9][A-Za-z0-9._@:-]*$'
    ),
    actor_id text NOT NULL CHECK (
        length(actor_id) BETWEEN 1 AND 128
        AND actor_id !~ '[[:cntrl:]]'
    ),
    request_sha256 text NOT NULL CHECK (request_sha256 ~ '^[0-9a-f]{64}$'),
    recorded_at_utc_ticks bigint NOT NULL CHECK (recorded_at_utc_ticks > 0),
    recorded_at_utc timestamptz NOT NULL
);

CREATE TABLE normacase.batch_review_results (
    request_id text PRIMARY KEY REFERENCES normacase.batch_review_requests(request_id),
    result_json json NOT NULL CHECK (octet_length(result_json::text) BETWEEN 1 AND 1048576),
    result_sha256 text NOT NULL CHECK (result_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE TRIGGER batch_review_requests_no_mutation
BEFORE UPDATE OR DELETE ON normacase.batch_review_requests
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();

CREATE TRIGGER batch_review_results_no_mutation
BEFORE UPDATE OR DELETE ON normacase.batch_review_results
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
