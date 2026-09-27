#!/usr/bin/env bash
# Applies EF Core migrations to the LOCAL development database only (CLAUDE.md hard rule 10).
#
# Refuses to run when:
#   - the connection string's Host/Server is anything other than localhost/127.0.0.1/::1
#   - ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT is set to something other than Development
#
# Connection string: $ConnectionStrings__Migrator (runs as shelter_migrator), else the local default.
# Usage: ./scripts/db-migrate-local.sh [--check]   (--check: validate guards only, do not migrate)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
default_cs="Host=127.0.0.1;Port=${POSTGRES_PORT:-55432};Database=${SHELTER_DB:-shelter};Username=shelter_migrator;Password=${SHELTER_MIGRATOR_PASSWORD:-shelter_migrator_dev}"
cs="${ConnectionStrings__Migrator:-$default_cs}"

fail() { echo "db-migrate-local: REFUSED — $*" >&2; exit 1; }

for var in ASPNETCORE_ENVIRONMENT DOTNET_ENVIRONMENT; do
  value="${!var:-}"
  if [ -n "$value" ] && [ "$value" != "Development" ]; then
    fail "$var=$value (only Development is allowed)"
  fi
done

# Extract Host= or Server= (case-insensitive) from the Npgsql connection string.
hosts=$(printf '%s' "$cs" | tr ';' '\n' | sed -nE 's/^[[:space:]]*([Hh][Oo][Ss][Tt]|[Ss][Ee][Rr][Vv][Ee][Rr])[[:space:]]*=[[:space:]]*(.*)$/\2/p' | tr -d '[:space:]')
[ -n "$hosts" ] || fail "connection string has no Host/Server"

# Npgsql allows several comma-separated hosts, each optionally with :port. Every one must be local.
IFS=',' read -ra host_list <<< "$hosts"
for entry in "${host_list[@]}"; do
  host="$entry"
  if [[ "$host" == \[*\]* ]]; then host="${host#[}"; host="${host%%]*}";   # [::1]:5432
  elif [[ "$host" != *:*:* ]]; then host="${host%%:*}"; fi                 # localhost:5432
  host=$(printf '%s' "$host" | tr '[:upper:]' '[:lower:]')
  case "$host" in
    localhost|127.0.0.1|::1) ;;
    *) fail "host '$host' is not local" ;;
  esac
done

if [ "${1:-}" = "--check" ]; then
  echo "db-migrate-local: guards passed (host: $hosts)"
  exit 0
fi

# The migrations project is decided by ADR 0005 (DbContext strategy) and created in M1.
migrations_project="${SHELTER_MIGRATIONS_PROJECT:-}"
if [ -z "$migrations_project" ]; then
  if ! find "$repo_root/backend" -type d -name Migrations -not -path '*/bin/*' -not -path '*/obj/*' | grep -q .; then
    echo "db-migrate-local: no migrations project yet (arrives in M1 after ADR 0005) — nothing to apply."
    exit 0
  fi
  fail "migrations exist but SHELTER_MIGRATIONS_PROJECT is not set; set it (and update this script's default)"
fi

cd "$repo_root"
dotnet tool restore >/dev/null
dotnet tool run dotnet-ef database update \
  --project "$migrations_project" \
  --startup-project backend/Shelter.Host \
  --connection "$cs"
