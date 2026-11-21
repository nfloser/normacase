-- Ein Schema je Modul. Fallinhalte dürfen nur in "workflow" liegen und sind
-- dort befristet (siehe docs/03-datenschutz-und-datenhaltung.md).
CREATE SCHEMA IF NOT EXISTS intake;
CREATE SCHEMA IF NOT EXISTS knowledge;
CREATE SCHEMA IF NOT EXISTS workflow;
CREATE SCHEMA IF NOT EXISTS documents;
CREATE SCHEMA IF NOT EXISTS outbound;
CREATE SCHEMA IF NOT EXISTS audit;
CREATE SCHEMA IF NOT EXISTS identity;

COMMENT ON SCHEMA workflow IS 'Befristete Vorgaenge; Inhalte werden nach Rueckgabe geloescht';
COMMENT ON SCHEMA audit IS 'Nur anfuegende Entscheidungsnachweise ohne Fallinhalt';
