# 0017. Reference data: global system tables plus per-tenant override tables

- **Status:** Accepted
- **Date:** 2026-09-27

## Context

Reference lists (species, intake and outcome reasons, later breeds) need bilingual system values that every organization shares (architecture §11.2, ADR 0013). Organizations also need to rename a value, hide one they never use, or add their own. Hard rule 11 forbids generic engines. Hard rule 1 requires every tenant-owned row to carry `TenantId` under RLS.

## Decision

- **One list, two tables, owned by the list's module.**
  - **Global table** (`animals.species`, `movements.intake_reason`): `code` (PK), `label_fr`/`label_en` (`LocalizedText`, NOT NULL) and `sort_order`.
    - It has no `TenantId`, and the runtime role has `SELECT` only.
    - Values are seeded and changed only by migrations.
  - **Tenant table** (`<list>_override`): tenant-owned with RLS and a unique `(tenant_id, code)`.
    - A row with a global code relabels, reorders or hides (`is_hidden`) that value.
    - A row with a new code adds a tenant value.
- **Merge at read time:** `ReferenceList.Merge` in BuildingBlocks applies the current tenant's overrides and drops hidden values.
  - The list is ordered by `sort_order`, then `code`.
  - A request without a tenant gets the system list.
- **API shape:** `[{ code, label: { fr, en } }]`. Clients pick the label by UI locale; the admin app uses `useLocalize`. A locale switch never refetches.
- **Codes are stable, language-neutral identifiers** (`dog`, `owner_surrender`). Referencing rows store the code.
- **Shared parts are small building blocks, not an engine:** `LocalizedText`, `MapLocalizedText` and `ReferenceList`. Each list keeps its own entities, configuration and endpoint.

## Alternatives considered

- **One generic `reference_value` table keyed by list name.**
  - Fewer tables, but it is the generic engine hard rule 11 rules out.
  - It would cross module ownership (one table for every module's lists).
  - It would weaken per-list constraints and foreign keys.
- **Tenant-owned copies of every system value, created at provisioning.**
  - Simple reads, but fixes and new system values would need data migrations across every tenant.
  - Tenants would silently drift from the system list.
- **Nullable `tenant_id` in one table per list (global rows have NULL).**
  - It breaks the "every tenant-owned table has a non-null `TenantId`" rule.
  - It complicates RLS and the tenant-leading unique index.

## Consequences

- Positive:
  - System values are fixed in one place.
  - Overrides are isolated by RLS like any tenant data, and are audited by the existing interceptor.
  - Adding a list is two small entities, one configuration and one endpoint.
- Negative / accepted trade-offs:
  - Each list costs two tables and a little repeated code.
  - An override repeats both labels even when it only hides or reorders a value.
  - Global and tenant values share one code space. If a later system value reuses a code a tenant already added, that tenant's row becomes an override of it. Pick system codes carefully.
- Follow-ups:
  - M2: permissions for reading lists and for managing overrides (no write endpoints yet).
  - M3: referencing rows (animal species, intake reason) store the code and validate it against the merged list.
