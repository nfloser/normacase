CREATE SCHEMA IF NOT EXISTS normacase;

CREATE TABLE normacase.assessment_records (
    assessment_id text PRIMARY KEY,
    case_id text NOT NULL,
    knowledge_pack_id text NOT NULL,
    knowledge_release text NOT NULL,
    platform_version text NOT NULL,
    assessment_date date NOT NULL,
    recorded_at_utc timestamptz NOT NULL,
    recorded_at_utc_ticks bigint NOT NULL,
    record_format_version integer NOT NULL,
    record_json json NOT NULL,
    record_sha256 text NOT NULL
        CHECK (record_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE FUNCTION normacase.reject_assessment_record_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'assessment_records are append-only'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER assessment_records_no_update
BEFORE UPDATE ON normacase.assessment_records
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_assessment_record_mutation();

CREATE TRIGGER assessment_records_no_delete
BEFORE DELETE ON normacase.assessment_records
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_assessment_record_mutation();
