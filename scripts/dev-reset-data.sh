#!/usr/bin/env bash
# Wipes the module data of every organization in the LOCAL development database (ADR 0023), so the dev seed can start
# over. The runtime role cannot delete (append-only ledgers, archive instead of delete); this runs as shelter_migrator,
# which owns the tables, inside the local Docker Compose Postgres container only.
#
# Kept: organizations, accounts, staff memberships and invitations (schema platform), global reference tables, and
# migration history. Emptied: every other table with a tenant_id column (animals, people, locations, movements,
# overrides, audit events and keys, ...).
# For a completely empty database instead: docker compose -f infrastructure/docker/compose.yml down -v, then up -d,
# migrate, and restart the API (it recreates the demo organization and admin).
#
# Usage: ./scripts/dev-reset-data.sh [--yes]
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose=(docker compose -f "$repo_root/infrastructure/docker/compose.yml")
db="${SHELTER_DB:-shelter}"

for var in ASPNETCORE_ENVIRONMENT DOTNET_ENVIRONMENT; do
  value="${!var:-}"
  if [ -n "$value" ] && [ "$value" != "Development" ]; then
    echo "dev-reset-data: REFUSED — $var=$value (only Development is allowed)" >&2
    exit 1
  fi
done

# docker compose talks to whatever daemon the environment points at: only the local one is allowed.
if [ -n "${DOCKER_HOST:-}" ]; then
  echo "dev-reset-data: REFUSED — DOCKER_HOST is set ($DOCKER_HOST)" >&2
  exit 1
fi
context="$(docker context show 2>/dev/null || echo default)"
case "$context" in
  default|desktop-linux) ;;
  *) echo "dev-reset-data: REFUSED — docker context '$context' is not the local default" >&2; exit 1 ;;
esac

if [ "${1:-}" != "--yes" ]; then
  read -r -p "Wipe all module data in the local '$db' database? [y/N] " answer
  [ "$answer" = "y" ] || [ "$answer" = "Y" ] || exit 1
fi

# psql inside the compose container: always the local database, no client needed on the host.
"${compose[@]}" exec -T -e PGPASSWORD="${SHELTER_MIGRATOR_PASSWORD:-shelter_migrator_dev}" postgres \
  psql -h 127.0.0.1 -U shelter_migrator -d "$db" -v ON_ERROR_STOP=1 -q <<'SQL'
DO $$
DECLARE
  tables text;
BEGIN
  SELECT string_agg(format('%I.%I', c.table_schema, c.table_name), ', ')
    INTO tables
    FROM information_schema.columns c
    JOIN information_schema.tables t USING (table_schema, table_name)
   WHERE c.column_name = 'tenant_id'
     AND t.table_type = 'BASE TABLE'
     AND c.table_schema <> 'platform';
  IF tables IS NOT NULL THEN
    -- No CASCADE: every table referencing these is in the list, and nothing else may be emptied.
    EXECUTE 'TRUNCATE ' || tables;
    RAISE NOTICE 'Emptied: %', tables;
  END IF;
END $$;
SQL
echo "dev-reset-data: done"
