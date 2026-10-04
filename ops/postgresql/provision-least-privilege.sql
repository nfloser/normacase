\set ON_ERROR_STOP on
\getenv migration_password NORMACASE_DEMO_DB_MIGRATION_PASSWORD
\getenv runtime_password NORMACASE_DEMO_DB_RUNTIME_PASSWORD

SELECT format(
    'CREATE ROLE normacase_migrator LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT',
    :'migration_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'normacase_migrator')
\gexec
SELECT format(
    'ALTER ROLE normacase_migrator WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT',
    :'migration_password')
\gexec

SELECT format(
    'CREATE ROLE normacase_runtime LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT',
    :'runtime_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'normacase_runtime')
\gexec
SELECT format(
    'ALTER ROLE normacase_runtime WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT',
    :'runtime_password')
\gexec

SELECT format('REVOKE CONNECT, TEMPORARY ON DATABASE %I FROM PUBLIC', current_database())
\gexec
SELECT format(
    'GRANT CONNECT ON DATABASE %I TO %I, normacase_migrator, normacase_runtime',
    current_database(), current_user)
\gexec

CREATE SCHEMA IF NOT EXISTS normacase AUTHORIZATION normacase_migrator;
ALTER SCHEMA normacase OWNER TO normacase_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA normacase FROM PUBLIC;
GRANT USAGE ON SCHEMA normacase TO normacase_runtime;

DO $transfer_existing_objects$
DECLARE
    item record;
BEGIN
    FOR item IN
        SELECT c.relname,
               CASE c.relkind
                   WHEN 'S' THEN 'SEQUENCE'
                   WHEN 'v' THEN 'VIEW'
                   WHEN 'm' THEN 'MATERIALIZED VIEW'
                   ELSE 'TABLE'
               END AS object_type
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'normacase'
          AND c.relkind IN ('r', 'p', 'S', 'v', 'm')
          AND pg_get_userbyid(c.relowner) <> 'normacase_migrator'
    LOOP
        EXECUTE format(
            'ALTER %s %I.%I OWNER TO normacase_migrator',
            item.object_type, 'normacase', item.relname);
    END LOOP;

    FOR item IN
        SELECT p.oid::regprocedure AS identity
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'normacase'
          AND pg_get_userbyid(p.proowner) <> 'normacase_migrator'
    LOOP
        EXECUTE format('ALTER ROUTINE %s OWNER TO normacase_migrator', item.identity);
    END LOOP;
END
$transfer_existing_objects$;

ALTER DEFAULT PRIVILEGES FOR ROLE normacase_migrator IN SCHEMA normacase
    REVOKE ALL ON TABLES FROM PUBLIC;
ALTER DEFAULT PRIVILEGES FOR ROLE normacase_migrator IN SCHEMA normacase
    GRANT SELECT, INSERT ON TABLES TO normacase_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE normacase_migrator IN SCHEMA normacase
    REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;

REVOKE ALL ON ALL TABLES IN SCHEMA normacase FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA normacase FROM normacase_runtime;
GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA normacase TO normacase_runtime;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA normacase FROM PUBLIC, normacase_runtime;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA normacase FROM PUBLIC, normacase_runtime;
