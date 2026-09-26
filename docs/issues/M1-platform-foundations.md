# M1 — Platform Foundations

Goal: tenancy, RLS, audit, crypto-shredding, jobs, and bilingual reference data are in place and **proven by tests** before any business module is built.

**Owner note:** this milestone is highest risk. Daniel reviews every migration and every line in `BuildingBlocks/Tenancy` and `BuildingBlocks/Auditing`. Run the `tenancy-privacy-reviewer` agent on every M1 PR.

Prerequisite: ADR #5 (DbContext strategy) accepted.

Order: M1-1 → M1-2 → M1-3 → (M1-4, M1-5 in parallel) → M1-6 → M1-7.

---

## M1-1 Tenant model and TenantContext

**Docs:** architecture §2.3, §8.1, §8.4

**Acceptance criteria**
- [ ] `Organization` entity (tenant) in Platform module; global table, no personal data.
- [ ] `ITenantOwned` interface; persistence interceptor sets `TenantId` on insert and **rejects** inserts/updates where `TenantId` differs from the current context.
- [ ] Scoped `TenantContext` resolved per request. Until M2 (identity), a dev-only resolver reads a header **only in the Development environment**; the resolver is replaceable and fails closed elsewhere.
- [ ] EF global query filter on all `ITenantOwned` entities.
- [ ] Tests: insert sets TenantId; cross-tenant update rejected; no tenant context → operation fails.

---

## M1-2 PostgreSQL RLS with `SET LOCAL`

**Docs:** architecture §8.2, §8.3 (incl. "Tenant propagation to the database")

**Acceptance criteria**
- [ ] Runtime connections use `shelter_app`; migrations use `shelter_migrator`.
- [ ] Unit-of-work: every tenant-scoped request runs in an explicit transaction.
- [ ] Interceptor executes `SET LOCAL app.tenant_id = ...` at transaction start. No session-level `SET` anywhere.
- [ ] Migration helper that, for a given table: enables + forces RLS, creates the tenant policy using `current_setting('app.tenant_id', true)`, and fails closed when unset.
- [ ] One sample tenant-owned table (e.g. `platform.tenant_setting`) using the helper, with `TenantId`-leading index.
- [ ] Separate platform-admin connection/role path for cross-tenant operations (provisioning), not usable from normal endpoints.

---

## M1-3 Tenant isolation test suite

**Docs:** architecture §8.3, §27

**Acceptance criteria**
- [ ] Integration tests run against Testcontainers Postgres **as `shelter_app`**.
- [ ] **Pooled-connection test:** tenant A request then tenant B request on the same pooled physical connection; B sees none of A's rows.
- [ ] `IgnoreQueryFilters()` query still returns only current-tenant rows (RLS backstop proven).
- [ ] Raw SQL inside the unit of work is tenant-scoped; raw SQL with no tenant set returns zero rows.
- [ ] No tenant set → zero rows (fail closed), not an error that leaks data.
- [ ] A reusable test fixture/base class so every future module can add isolation tests in a few lines.

---

## M1-4 Background jobs with tenant restore

**Docs:** architecture §8.5, §13

**Acceptance criteria**
- [ ] Hangfire configured with PostgreSQL storage (its own schema).
- [ ] Tenant-aware job base: job payload carries `TenantId`; context and `SET LOCAL` restored before any data access; job without TenantId fails.
- [ ] Test: job enqueued for tenant A cannot read tenant B data.
- [ ] Hangfire dashboard restricted to platform admins (dev: local only).

---

## M1-5 Audit capture

**Docs:** architecture §7.1, §17.1, §17.3, §17.4

**Acceptance criteria**
- [ ] `AuditEvent` table (tenant-scoped, RLS) with fields per §17.1.
- [ ] Runtime role has `INSERT` + `SELECT` only on audit table (no `UPDATE`/`DELETE`).
- [ ] `SaveChangesInterceptor` records create/update/delete for tracked entities with actor, correlation ID, source.
- [ ] Field classification mechanism (personal vs non-personal) available to entity configurations.
- [ ] Explicit API for emitting audit events from non-EF paths (raw SQL, jobs, integrations).
- [ ] Tests: update produces audit row with before/after for non-personal fields; audit rows cannot be modified by runtime role.

---

## M1-6 Crypto-shredding for personal data

**Docs:** architecture §17.2

**Acceptance criteria**
- [ ] Per-subject data key store (`PersonDataKey`), keys encrypted by a master key via an `IKeyProvider` interface. Local dev: file/env master key. Production: KMS implementation stub + ADR note.
- [ ] Personal-classified values in audit payloads are encrypted with the subject's data key.
- [ ] `ShredSubject(subjectId)` destroys the data key and is itself audited.
- [ ] Tests: before shred, authorized reader can decrypt; after shred, audit rows still exist (actor, action, field names, timestamps) but personal values are unrecoverable.
- [ ] Architecture/unit test fails if an entity linked to a person has unclassified fields.

---

## M1-7 Bilingual reference data

**Docs:** architecture §11.2

**Acceptance criteria**
- [ ] `LocalizedText` value object (`Fr`, `En`) mapped to two columns; both required for system reference data.
- [ ] Reference-data pattern supporting global system values with optional tenant overrides/additions (design documented in module doc or ADR).
- [ ] Seed sample: Species and one reason list, in fr-CA and en-CA (Québec terminology; flag uncertain terms `TODO(fr-review)`).
- [ ] API returns both labels; admin app renders by UI locale via a shared helper.
- [ ] Test: switching UI locale switches label without refetch.

---

## M1 exit criteria

- [ ] All isolation tests green in CI.
- [ ] `tenancy-privacy-reviewer` reports no Critical/High findings on M1 code.
- [ ] `docs/modules/platform.md` exists and describes tenancy, audit, crypto-shredding, reference data.
- [ ] Daniel has personally reviewed all M1 migrations.

Next: M2 (identity, memberships, permissions), then M3 (People, Animals, Locations, Movements + timeline).
