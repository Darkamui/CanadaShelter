#!/usr/bin/env bash
# Fills the local organization with sample data through POST /api/platform/dev/seed (ADR 0023).
# The API must run in Development with DevSeed:Enabled (the default in appsettings.Development.json).
#
# Usage: ./scripts/dev-seed.sh ['<json body>']
#   ./scripts/dev-seed.sh                                     # demo scale (factor 10) in the admin's organization
#   ./scripts/dev-seed.sh '{"scale":"small"}'                 # small | demo | load (1 | 10 | 100)
#   ./scripts/dev-seed.sh '{"factor":25,"seed":42}'           # explicit factor (1-200), reproducible seed
#   ./scripts/dev-seed.sh '{"newOrganization":true}'          # a fresh organization, the caller as administrator
#
# Environment: SHELTER_API (default http://localhost:5080), DEV_SEED_EMAIL / DEV_SEED_PASSWORD (default: the
# DevSeed admin from appsettings.Development.json), DEV_SEED_ORGANIZATION (the organization ID to act in; needed once
# the account belongs to several, for example after newOrganization — otherwise its first organization is used).
set -euo pipefail

api="${SHELTER_API:-http://localhost:5080}"
email="${DEV_SEED_EMAIL:-admin@refuge.local}"
password="${DEV_SEED_PASSWORD:-refuge-demo-admin}"
body="${1:-{\}}"

# Exact host match: the credentials must never leave the machine (localhost.example.org is not local).
if ! [[ "$api" =~ ^https?://(localhost|127\.0\.0\.1|\[::1\])(:[0-9]+)?/?$ ]]; then
  echo "dev-seed: REFUSED — $api is not local" >&2
  exit 1
fi
api="${api%/}"

jar="$(mktemp)"
trap 'rm -f "$jar"' EXIT

xsrf() {
  curl -fsS -c "$jar" -b "$jar" "$api/api/platform/session/antiforgery" >/dev/null
  awk '$6 == "XSRF-TOKEN" { print $7 }' "$jar" | tail -n 1
}

token="$(xsrf)"
curl -fsS -c "$jar" -b "$jar" -H "Content-Type: application/json" -H "X-XSRF-TOKEN: $token" \
  -d "{\"email\":\"$email\",\"password\":\"$password\"}" "$api/api/platform/session/login" >/dev/null

# The antiforgery token is bound to the signed-in user: fetch a new one after login.
token="$(xsrf)"

# With several memberships, login selects none: pick DEV_SEED_ORGANIZATION, else the first one.
session="$(curl -fsS -c "$jar" -b "$jar" "$api/api/platform/session")"
organization="${DEV_SEED_ORGANIZATION:-}"
if [ -z "$organization" ] && printf '%s' "$session" | grep -q '"activeOrganizationId":null'; then
  organization="$(printf '%s' "$session" | grep -o '"organizationId":"[^"]*"' | head -n 1 | cut -d'"' -f4)"
fi
if [ -n "$organization" ]; then
  echo "dev-seed: acting in organization $organization" >&2
  curl -fsS -c "$jar" -b "$jar" -X POST -H "X-XSRF-TOKEN: $token" \
    "$api/api/platform/session/organization/$organization" >/dev/null
  token="$(xsrf)"
fi
echo "dev-seed: seeding with $body (this can take a while at large scales)" >&2
curl -fsS --max-time 1800 -c "$jar" -b "$jar" -H "Content-Type: application/json" -H "X-XSRF-TOKEN: $token" \
  -d "$body" "$api/api/platform/dev/seed"
echo
