-- Arbeitslisten wie im bisherigen Fallexplorer: Unterlagensichtung ohne Regelwerk und technische Klärung vor der
-- Bewertung. Vorgänge ohne Regelwerk haben kein Knowledge Release; der Fehlercode ist ein technischer Bezeichner
-- ohne Fallinhalt und wird mit dem Vorgang gelöscht.
ALTER TABLE workflow.process ALTER COLUMN knowledge_release DROP NOT NULL;
ALTER TABLE workflow.process ADD COLUMN technical_error text
    CHECK (technical_error IS NULL OR technical_error IN ('UNKNOWN_RELEASE', 'INVALID_CASE_INPUT'));

-- Bisher lagen widersprüchliche Vorgänge (HUMAN_REVIEW) mit den unvollständigen in den Rückfragen.
UPDATE workflow.process SET state = 'IN_CONFLICT_REVIEW', queue = 'conflict'
WHERE state = 'IN_CLARIFICATION' AND outcome = 'HUMAN_REVIEW';
