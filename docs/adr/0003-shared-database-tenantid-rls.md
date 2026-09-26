# 0003. Shared database + `TenantId` + EF filters + RLS

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Every organization is a tenant (§2.3, §8.1). A cross-tenant leak of animal, person or case data is the worst failure this product can have. At the same time, we expect many small tenants, and per-tenant infrastructure would be too costly to run (§8.2, §8.3).

## Decision

- **Storage model:** one shared database with shared per-module schemas. Every tenant-owned row has a `TenantId`. Global reference data has none.
- **Isolation layers:**
  1. `TenantContext` resolved from the authenticated request. It never comes from the request body (hard rule 2).
  2. Server-side, permission-based authorization.
  3. EF Core global query filters on `ITenantOwned`.
  4. PostgreSQL RLS: `ENABLE` + `FORCE ROW LEVEL SECURITY` and a tenant policy on every tenant-owned table.
  5. Indexes and unique constraints that start with `TenantId`.
- **Database roles:**
  - Runtime role `shelter_app`: `NOBYPASSRLS`, owns no tables.
  - Migration role `shelter_migrator`: owns the schema.
  - Cross-tenant platform operations will use a third, separate role.
- **Tests:** integration tests run as the runtime role (M0-2 roles, M0-3 fixture).

## Alternatives considered

- **Database per tenant**: strongest isolation. But migrations, backups and connection pools grow with every tenant, and cross-tenant platform reporting gets hard.
- **Schema per tenant**: the same migration fan-out, plus a catalog explosion in PostgreSQL.
- **Application filters only, no RLS**: one missed filter, raw SQL query or Dapper report leaks data. RLS is the backstop.

## Consequences

- Positive: cheap tenants. Defence in depth: a bug in one layer isn't a breach.
- Negative / accepted trade-offs:
  - Every migration for a tenant-owned table must add RLS and `TenantId`-leading indexes (`/tenancy-check`).
  - Noisy neighbours share one database.
  - Tenant export and deletion are row-level operations, not "drop the database".
- Follow-ups:
  - M1: RLS policy helper and a cross-tenant denial test for every feature.
  - Separate platform-admin role and connection string.
