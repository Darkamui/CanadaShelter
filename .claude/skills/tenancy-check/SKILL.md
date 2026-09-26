---
name: tenancy-check
description: Checklist review of new or changed entities/tables/endpoints for tenant isolation, RLS, audit classification, bilingual fields, and public exposure. Use whenever persistence, migrations, or public endpoints change.
---

# /tenancy-check

Scope: the current diff (`git diff main...HEAD`). Check only changed entities, migrations, endpoints, and jobs.

For each item, answer **PASS / FAIL / N/A** with file:line evidence. Do not fix anything during the check; list fixes after.

## Entities & tables

1. Tenant-owned entity implements `ITenantOwned` and has non-null `TenantId`.
2. Intentionally global table (reference/system data) is justified in a comment and has no personal data.
3. Migration enables **and forces** RLS and creates the tenant policy using the helper.
4. Every index and unique constraint on a tenant-owned table **starts with `TenantId`**.
5. No foreign key or navigation property into another module's tables (IDs only).
6. Personal fields are classified for audit/crypto-shredding. None left unclassified.
7. User-facing labels use `LocalizedText` (fr + en).

## Access paths

8. `TenantId` is never read from a request body, query string, or route for authorization.
9. Every new endpoint declares a permission; default deny holds.
10. Raw SQL/Dapper queries run inside the tenant transaction (`SET LOCAL` applied) — no connection opened outside the unit of work.
11. Background jobs carry `TenantId` in the payload and restore tenant context before data access.
12. Object storage keys use `tenants/{tenantId}/...`; downloads go through authorized signed URLs.
13. Public (anonymous) endpoints expose only fields explicitly marked public.
14. No personal values in log messages or exception messages.

## Tests

15. Cross-tenant denial test exists for each new read and write path.
16. Permission-denied test exists for each new endpoint.
17. Integration tests run as the runtime DB role (RLS active), not the migration role.

## Output

A table of the 17 items, then a short list of required fixes (FAIL items) in priority order.
