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

## Amendment 1 (2026-10-02, M3-3): list attributes

- **A list may carry typed attributes** next to its labels, when generic code must act on a value. The first are `holds_animals` on `operations.location_kind` (M3-3) and `sac_category` on intake reasons and outcome types (M3-5).
- **Both tables carry the attribute, NOT NULL.** The override row always gives it, just as it always gives both labels. The override value wins.
  - A tenant value under a new code therefore always has the attribute.
  - Relabelling a system value repeats its attribute. A later change to the system value does not reach that tenant (the same trade-off as for labels).
- **Hidden values keep their attributes.** Hiding only removes a value from new choices. Existing rows that store the code keep their meaning, so `holds_animals` and labels are still resolved from the merged list, hidden values included. Endpoints validate new input against the visible values only.
- Alternatives considered:
  - Nullable attribute on the override, meaning "inherit": tenant values would need a separate NOT NULL rule, and every read would need coalescing.
  - Attributes only on the global table: a tenant value could never hold animals or count in a SAC category.
- **Applied in M3-5** (migration `AddMovementLedger`): `sac_category` on intake reasons and outcome types, with the categories as code constants (`Movements/Domain/SacCategories.cs`, `TODO(pilot-review)`). Categories drive rules, not labels: adoption and return to owner require a person; died and euthanasia are terminal. Tenant intake reason overrides that existed before the migration got `other_intake`, because RLS hides them from the migrator; a tenant must recategorize them.
