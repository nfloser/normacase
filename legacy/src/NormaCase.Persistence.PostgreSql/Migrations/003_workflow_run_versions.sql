CREATE TABLE normacase.workflow_run_versions (
    run_id text NOT NULL CHECK (length(btrim(run_id)) > 0),
    revision bigint NOT NULL CHECK (revision >= 0),
    case_id text NOT NULL CHECK (length(btrim(case_id)) > 0),
    knowledge_pack_id text NOT NULL,
    knowledge_release text NOT NULL,
    workflow_id text NOT NULL,
    workflow_version integer NOT NULL CHECK (workflow_version > 0),
    state_id text NOT NULL,
    platform_version text NOT NULL,
    recorded_at_utc timestamptz NOT NULL,
    recorded_at_utc_ticks bigint NOT NULL,
    run_format_version integer NOT NULL,
    run_json json NOT NULL,
    run_sha256 text NOT NULL CHECK (run_sha256 ~ '^[0-9a-f]{64}$'),
    PRIMARY KEY (run_id, revision)
);

CREATE FUNCTION normacase.reject_workflow_run_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'workflow run versions are append-only'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER workflow_runs_no_update
BEFORE UPDATE ON normacase.workflow_run_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_workflow_run_mutation();

CREATE TRIGGER workflow_runs_no_delete
BEFORE DELETE ON normacase.workflow_run_versions
FOR EACH ROW
EXECUTE FUNCTION normacase.reject_workflow_run_mutation();
