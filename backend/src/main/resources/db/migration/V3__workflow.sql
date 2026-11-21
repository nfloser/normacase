-- Eingangsquittungen für idempotente Einlieferung (ohne Fallinhalt).
CREATE TABLE intake.receipt (
    submitted_by     text        NOT NULL,
    idempotency_key  text        NOT NULL,
    request_sha256   text        NOT NULL,
    process_id       uuid        NOT NULL,
    received_at      timestamptz NOT NULL,
    PRIMARY KEY (submitted_by, idempotency_key)
);

-- Befristete Vorgänge. Fallinhalte (case_input, assessment) liegen nur hier und werden beim Abschluss oder
-- spätestens mit expires_at gelöscht (docs/03-datenschutz-und-datenhaltung.md).
CREATE TABLE workflow.process (
    process_id        uuid PRIMARY KEY,
    external_system   text        NOT NULL,
    external_case_id  text        NOT NULL,
    submitted_by      text        NOT NULL,
    knowledge_release text        NOT NULL,
    state             text        NOT NULL,
    queue             text        NOT NULL,
    outcome           text        NOT NULL,
    case_input        jsonb,
    assessment        jsonb,
    evidence_id       uuid,
    confirmed_by      text,
    received_at       timestamptz NOT NULL,
    updated_at        timestamptz NOT NULL,
    expires_at        timestamptz NOT NULL,
    version           integer     NOT NULL DEFAULT 0
);
CREATE INDEX process_queue_idx ON workflow.process (queue, received_at);
CREATE INDEX process_expiry_idx ON workflow.process (expires_at);

-- Identifikationsdaten getrennt von den fachlichen Daten (vgl. § 276 Abs. 2 SGB V).
CREATE TABLE workflow.process_subject (
    process_id    uuid PRIMARY KEY REFERENCES workflow.process ON DELETE CASCADE,
    display_name  text NOT NULL
);

-- Bearbeitungsverlauf des Vorgangs; nur Aktionscodes, keine Freitexte. Wird mit dem Vorgang gelöscht,
-- der dauerhafte Nachweis liegt in audit.process_evidence.
CREATE TABLE workflow.review_event (
    event_id    bigserial PRIMARY KEY,
    process_id  uuid        NOT NULL REFERENCES workflow.process ON DELETE CASCADE,
    at          timestamptz NOT NULL,
    actor       text        NOT NULL,
    action      text        NOT NULL,
    reason      text
);

CREATE TABLE documents.document (
    process_id   uuid    NOT NULL,
    document_id  text    NOT NULL,
    title        text    NOT NULL,
    media_type   text    NOT NULL,
    sha256       text    NOT NULL,
    size_bytes   integer NOT NULL,
    page_count   integer NOT NULL,
    content      bytea   NOT NULL,
    PRIMARY KEY (process_id, document_id)
);

CREATE TABLE documents.page (
    process_id   uuid    NOT NULL,
    document_id  text    NOT NULL,
    page         integer NOT NULL,
    content      bytea   NOT NULL,
    PRIMARY KEY (process_id, document_id, page),
    FOREIGN KEY (process_id, document_id) REFERENCES documents.document ON DELETE CASCADE
);

CREATE TABLE outbound.message (
    message_id        uuid PRIMARY KEY,
    process_id        uuid        NOT NULL,
    recipient         text        NOT NULL,
    external_system   text        NOT NULL,
    external_case_id  text        NOT NULL,
    payload           jsonb,
    created_at        timestamptz NOT NULL,
    acknowledged_at   timestamptz
);
CREATE INDEX message_pending_idx ON outbound.message (recipient) WHERE acknowledged_at IS NULL;

-- Nachweis jeder Bearbeitungshandlung ohne Inhalt.
CREATE TABLE audit.process_evidence (
    evidence_id       uuid PRIMARY KEY,
    recorded_at       timestamptz NOT NULL,
    process_id        uuid        NOT NULL,
    external_system   text        NOT NULL,
    external_case_id  text        NOT NULL,
    action            text        NOT NULL,
    actor             text        NOT NULL,
    outcome           text,
    reason            text
);
CREATE INDEX process_evidence_process_idx ON audit.process_evidence (process_id);

CREATE TRIGGER process_evidence_append_only
    BEFORE UPDATE OR DELETE ON audit.process_evidence
    FOR EACH ROW EXECUTE FUNCTION audit.reject_change();
