-- Register geladener Knowledge Releases: dieselbe Release-ID darf nie anderen Inhalt bekommen.
CREATE TABLE knowledge.release (
    release_id        text PRIMARY KEY,
    pack_id           text        NOT NULL,
    content_sha256    text        NOT NULL,
    validation_level  text        NOT NULL,
    lifecycle_status  text        NOT NULL,
    first_loaded_at   timestamptz NOT NULL DEFAULT now()
);

-- Entscheidungsnachweis ohne Fallinhalt (docs/03-datenschutz-und-datenhaltung.md, Abschnitt 2).
-- Es gibt bewusst keine Spalten für Werte, Freitexte, Namen oder Dokumente.
CREATE TABLE audit.decision_evidence (
    evidence_id         uuid PRIMARY KEY,
    recorded_at         timestamptz NOT NULL,
    external_system     text,
    external_case_id    text,
    input_sha256        text        NOT NULL,
    platform_version    text        NOT NULL,
    knowledge_release   text        NOT NULL,
    knowledge_sha256    text        NOT NULL,
    assessment_date     date        NOT NULL,
    rule_id             text,
    rule_version        integer,
    outcome             text        NOT NULL,
    criteria            jsonb       NOT NULL,
    actor               text        NOT NULL,
    actor_kind          text        NOT NULL
);

CREATE INDEX decision_evidence_external_idx ON audit.decision_evidence (external_system, external_case_id);

-- Nur anfügend: Änderungen und Löschungen sind technisch ausgeschlossen. Löschfristen laufen über einen
-- eigenen, protokollierten Weg (Phase 4).
CREATE FUNCTION audit.reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit.% ist nur anfügend', TG_TABLE_NAME;
END;
$$;

CREATE TRIGGER decision_evidence_append_only
    BEFORE UPDATE OR DELETE ON audit.decision_evidence
    FOR EACH ROW EXECUTE FUNCTION audit.reject_change();
