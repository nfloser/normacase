#!/bin/sh
set -eu

: "${NORMACASE_DEMO_DB_MIGRATION_PASSWORD:?Set an external migration-role password}"
: "${NORMACASE_DEMO_DB_RUNTIME_PASSWORD:?Set an external runtime-role password}"

psql --set=ON_ERROR_STOP=1 \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --file /opt/normacase/provision-least-privilege.sql
