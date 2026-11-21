CREATE TABLE normacase.intake_streams (
    source_system_id text NOT NULL,
    upstream_case_id text NOT NULL,
    case_id text NOT NULL UNIQUE,
    case_type_id text NOT NULL,
    PRIMARY KEY (source_system_id,upstream_case_id)
);
CREATE TABLE normacase.intake_records (
    source_system_id text NOT NULL,
    upstream_case_id text NOT NULL,
    upstream_revision bigint NOT NULL CHECK (upstream_revision > 0),
    record_json json NOT NULL,
    record_sha256 text NOT NULL CHECK (record_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY(source_system_id,upstream_case_id,upstream_revision),
    FOREIGN KEY(source_system_id,upstream_case_id) REFERENCES normacase.intake_streams
);
CREATE TABLE normacase.intake_messages (
    source_system_id text NOT NULL,
    message_id text NOT NULL,
    upstream_case_id text NOT NULL,
    upstream_revision bigint NOT NULL,
    PRIMARY KEY(source_system_id,message_id),
    FOREIGN KEY(source_system_id,upstream_case_id,upstream_revision) REFERENCES normacase.intake_records
);
CREATE TRIGGER intake_streams_no_mutation BEFORE UPDATE OR DELETE ON normacase.intake_streams
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER intake_records_no_mutation BEFORE UPDATE OR DELETE ON normacase.intake_records
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
CREATE TRIGGER intake_messages_no_mutation BEFORE UPDATE OR DELETE ON normacase.intake_messages
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
