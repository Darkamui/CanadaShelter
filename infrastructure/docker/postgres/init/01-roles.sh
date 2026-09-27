#!/usr/bin/env bash
# Creates the application database and its two roles (architecture §8.3).
# Runs once, as the container superuser, on an empty data directory.
# Also mounted by the integration-test Postgres container so tests match local.
#
#   shelter_migrator  owns the database and schemas; runs migrations only.
#   shelter_app       runtime role: no BYPASSRLS, owns nothing, CONNECT only.
#                     Table/schema grants are added by migrations (M1).
set -euo pipefail

: "${SHELTER_DB:=shelter}"
: "${SHELTER_MIGRATOR_PASSWORD:?SHELTER_MIGRATOR_PASSWORD is required}"
: "${SHELTER_APP_PASSWORD:?SHELTER_APP_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v db="$SHELTER_DB" \
  -v migrator_pw="$SHELTER_MIGRATOR_PASSWORD" \
  -v app_pw="$SHELTER_APP_PASSWORD" <<'SQL'
CREATE ROLE shelter_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
  PASSWORD :'migrator_pw';
CREATE ROLE shelter_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT
  PASSWORD :'app_pw';

CREATE DATABASE :"db" OWNER shelter_migrator;
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE :"db" TO shelter_app;

\connect :"db"

-- Search (architecture §12). Created by the superuser so migrations never need to.
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- public holds only extension functions: no one but the migrator may create objects there.
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO shelter_migrator;
GRANT USAGE ON SCHEMA public TO shelter_app;
SQL
