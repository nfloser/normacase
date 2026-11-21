-- Least Privilege: Migrationen laufen als Eigentümer, die Anwendung als normacase_app.
-- Das Konto wird im Betrieb mit LOGIN und Passwort angelegt (Compose: ops/postgres/init.sql);
-- fehlt es, entsteht es hier ohne Anmelderecht, damit die Rechte trotzdem vergeben sind.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'normacase_app') THEN
        CREATE ROLE normacase_app NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA intake, knowledge, workflow, documents, outbound, audit TO normacase_app;

-- Quittungen, Release-Register und Nachweise: nur lesen und anfügen.
GRANT SELECT, INSERT ON intake.receipt TO normacase_app;
GRANT SELECT, INSERT ON knowledge.release TO normacase_app;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA audit TO normacase_app;

-- Befristete Inhalte: lesen, schreiben, löschen.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA workflow TO normacase_app;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA workflow TO normacase_app;
GRANT SELECT, INSERT, DELETE ON ALL TABLES IN SCHEMA documents TO normacase_app;
GRANT SELECT, INSERT, UPDATE ON outbound.message TO normacase_app;

-- Künftige Tabellen des Eigentümers bekommen dieselben Grundrechte; Nachweise weiterhin nur anfügen.
ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT SELECT, INSERT ON TABLES TO normacase_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA workflow GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO normacase_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA workflow GRANT USAGE ON SEQUENCES TO normacase_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA documents GRANT SELECT, INSERT, DELETE ON TABLES TO normacase_app;
