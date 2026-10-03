# M3 — Core Records: People, Locations, Animals, Movements

Goal: staff can record the people, locations and animals of a shelter, and every change of custody (intake, relocation, outcome) goes through an **append-only movement ledger**. In the same transaction, the ledger updates the animal's current summary and its timeline (architecture §7.1). Lists are paged and searchable, in fr-CA and en-CA. As in M1 and M2, the invariants are **proven by tests**.

**Owner note:** People holds the first large set of personal data. Daniel reviews every migration, the audit classification of every new entity, and ADR 0021. Run the `tenancy-privacy-reviewer` agent on every sub-issue that touches persistence.

Prerequisites:

- M2 merged (PR #3).
- New dependency approved: `@tanstack/react-table` (admin app only). No other package.
- ADR 0021 (in-process module contracts) accepted before M3-5. The ADR 0017 amendment (reference list attributes) is accepted with M3-3.

Order: M3-1 → M3-2 → M3-3 → M3-4 → M3-5 → M3-6. One branch (`m3/core-records`), one commit per sub-issue, one PR.

Cross-cutting rules:

- **Contracts.**
  - Modules call each other only through interfaces in `*.Contracts`, implemented inside the owning module (ADR 0021).
  - Contract methods stage changes and never save. The calling endpoint saves once, inside the unit of work.
- **Grants.**
  - No DELETE on any M3 table.
  - `movements.movement` and `animals.timeline_event` are SELECT/INSERT only, so the database itself enforces append-only.
- **Paging.**
  - Offset paging: `page` ≥ 1, `pageSize` 1–100 (default 25).
  - Responses are `{ items, page, pageSize, totalCount }`, with `Id` as the tie-breaker in every sort.
- **Search.**
  - Search runs on stored normalized columns (no accents, œ → oe, lower case) with `pg_trgm` indexes.
  - Language-specific full-text search (`fr_unaccent`, ADR 0014) waits for the first free-text feature.
- **Reference lists.**
  - Input is validated against the tenant's merged list, where hidden codes are refused.
  - Stored codes still display after an entry is hidden.

---

## M3-1 List foundations

**Docs:** architecture §12, product-spec §36

**Acceptance criteria**

- [x] `PageRequest` / `PagedResult<T>` in `BuildingBlocks/Persistence/Paging`. Out-of-range values are clamped.
- [x] `SearchNormalizer` in BuildingBlocks: `Text()` (accents, ligatures, case, spaces), `Digits()` (phone numbers) and `Identifier()` (microchips and licences: alphanumerics only, upper case).
- [x] `TenantHarness.AssertIsolatedAsync` passes on tables that deny UPDATE or DELETE to the runtime role (`42501` counts as a denied write).
- [x] `packages/ui`:
  - `DataTable` on `@tanstack/react-table`: server-side paging and sorting; loading, empty and error states; labels passed in for i18n.
  - `Dialog` and `Select` on the existing `radix-ui`.
- [x] `useListParams` in the admin app keeps page, size, sort and query in the URL.
- [x] Tests: normalizer and paging (unit); harness self-test on a SELECT/INSERT-only table; DataTable paging and accessible names (Vitest).

---

## M3-2 People

**Docs:** architecture §6.2, §16, §17; product-spec §5.2

**Acceptance criteria**

- [x] Tenant-owned `Person` (schema `people`):
  - Fields:
    - names and display name;
    - email, phone and secondary phone;
    - address (line, city, province, postal code);
    - preferred language (French by default; stored as `fr`/`en`, the same codes as Platform users);
    - role tags (owner, adopter, foster, volunteer, donor, finder, surrenderer);
    - notes and archived.
  - Audit: the audit subject is the person, and every field is personal, including the normalized search columns.
  - RLS and indexes starting with `tenant_id`. The name uses a trigram index; the reason it cannot lead with `tenant_id` is recorded in the migration.
- [x] Permissions `person.read` and `person.write`.
- [x] Endpoints:
  - a paged list, with search by name, email or phone and filters for role and archived;
  - get, create, update, archive and unarchive.
- [x] Create returns `possibleDuplicates` (same normalized email or phone) without blocking.
- [x] Contract `IPersonDirectory`: `GetSummariesAsync(ids)`, `ExistsActiveAsync(id)`.
- [x] Admin screens: list, detail and form (`useZodForm`), in fr-CA and en-CA.
- [x] Tests:
  - tenant isolation, and cross-tenant GET/PUT → 404;
  - 403 without the permission;
  - classification rule;
  - accent-insensitive search;
  - the duplicate hint.

---

## M3-3 Locations

**Docs:** architecture §6.5; product-spec §9; ADR 0017

**Acceptance criteria**

- [x] `location_kind` reference list:
  - A global bilingual list plus a tenant override, with a `holds_animals` attribute.
  - Seeded kinds: shelter, building, room, kennel, cage, clinic, isolation, external clinic, partner shelter, field.
  - ADR 0017 amendment: lists can carry attributes, and a code added by an override must give them.
- [x] Tenant-owned `Location` (Operations, schema `operations`):
  - Fields: parent, kind, name (plain text), optional capacity, archived.
  - Names are unique among active siblings.
  - The tree never forms a cycle (a per-tenant advisory lock, then a recursive check).
- [x] Permissions `location.read` and `location.write`.
- [x] Endpoints: tree, kinds, create, update (including moving under another parent), archive.
- [x] Contract `ILocationDirectory`: `GetAsync(ids)`, `GetSubtreeIdsAsync(root)`, `IsActiveHoldingAsync(id)`.
- [x] Admin screen: a location tree with create and edit dialogs.
- [x] Tests:
  - tenant isolation (location and override);
  - a cycle is rejected;
  - an unknown or hidden kind is rejected;
  - a parent from another tenant is rejected;
  - 403 without the permission.

---

## M3-4 Animal record and timeline

**Docs:** architecture §6.1, §7.1, §12; product-spec §7

**Acceptance criteria**

- [x] Tenant-owned `Animal`:
  - Fields:
    - a per-tenant sequential number, name, species (reference list);
    - breed, secondary breed and colour (plain text);
    - sex, reproductive status, birth date (with an "estimated" flag);
    - marks and alert texts (behaviour, medical, legal).
  - Alert texts are non-personal, and the UI says not to enter personal information.
  - Custody summary: status `not_in_care | in_care | outcome`, location, in care since, current intake and last outcome. Only the Movements contract writes it.
  - Edits use optimistic concurrency (`xmin`): a stale edit → 409.
- [x] `AnimalIdentifier` (microchip, licence, external). An active microchip is unique per tenant.
- [x] Append-only `timeline_event`:
  - Fields: animal, type, time, source module and record, and parameters made of codes and IDs only (no personal data).
  - Creating an animal records `animal_registered`.
  - Names are resolved when the timeline is read. Person names (and person IDs) are shown only with `person.read`.
- [x] Permission `animal.write`, which joins `animal.read`.
- [x] Endpoints:
  - a paged search by name, number or microchip, with filters for status, species and location;
  - get, create, update;
  - add and deactivate identifiers;
  - timeline.
- [x] Contracts `IAnimalCustody` (lock and compare-and-set of the summary), `IAnimalTimeline` and `IAnimalPopulation`. ADR 0021 written.
- [x] Admin screens: list, create, and a detail page with identifiers and the timeline.
- [x] Tests:
  - tenant isolation for every table;
  - concurrent creates get distinct sequential numbers;
  - a duplicate active microchip → 400, while the same chip in another tenant is accepted;
  - the runtime role cannot UPDATE a timeline row;
  - a hidden species is refused on create but still displays;
  - 403 without the permission.

---

## M3-5 Movements ledger and custody

**Docs:** architecture §6.3, §7.1; product-spec §8; ADR 0021

**Acceptance criteria**

- [ ] `outcome_type` reference list (global plus override): adoption, return to owner, transfer out, return to field, died, euthanized, other.
  - Intake reasons and outcome types get a `sac_category` (Shelter Animals Count), added by a new migration, marked `TODO(pilot-review)`.
- [ ] Append-only `movement`:
  - Types: intake, relocation, outcome, void.
  - Fields: reason or outcome code, from and to location, person, notes, occurred at, recorded by, and the amended movement.
  - `notes` is personal, with the person as the audit subject.
- [ ] Rules (owned by Movements):
  - An intake needs an animal that is not in care. A relocation or outcome needs an animal in care.
  - The target location is active and holds animals.
  - The person exists and is active. A person is required for adoption and return to owner.
  - `occurred_at` is not before the latest movement still in effect, and not in the future (5 minute tolerance).
- [ ] Every movement updates the animal summary and appends a timeline event **in the same transaction**. Two concurrent intakes on one animal → one succeeds, one gets 409.
- [ ] Void:
  - It applies only to the latest movement still in effect, and needs a reason and `movement.amend` (sensitive, administrator only).
  - It is a new row. The summary is rebuilt from the ledger.
- [ ] Permissions `movement.write` and `movement.amend`.
- [ ] Endpoints: record an intake, relocation or outcome; void; an animal's history; outcome types.
- [ ] A location with animals cannot be archived (409).
- [ ] Admin screens: an intake flow (pick or create the animal, then the intake), move and outcome dialogs, and history on the animal page.
- [ ] Tests:
  - all-or-nothing when a failure is forced after staging;
  - the concurrent double intake;
  - a void restores the previous state;
  - the runtime role cannot UPDATE or DELETE a movement;
  - `movement.amend` → 403 for `staff`;
  - archiving an occupied location → 409;
  - tenant isolation.

---

## M3-6 Operational views, E2E and docs

**Docs:** product-spec §9, §41.2

**Acceptance criteria**

- [ ] Population by location: a count per location plus subtree totals.
- [ ] The animals in a location subtree, shown on a location page.
- [ ] Playwright, in fr-CA and en-CA with the API mocked: location → person → animal → intake → move → outcome → timeline.
- [ ] Module docs: `people.md` and `operations.md` are new; `animals.md`, `movements.md` and `platform.md` are updated. French terms to review are listed.

---

## M3 exit criteria

- [ ] Every new tenant-owned table has RLS, indexes starting with `tenant_id` (or a documented exception), and an isolation test.
- [ ] The ledger, summary and timeline are proven atomic, and the ledger is append-only at the database level.
- [ ] `tenancy-privacy-reviewer` reports no Critical/High findings on M3 code.
- [ ] Every new personal field is classified. No personal data appears in logs, job arguments or timeline parameters.
- [ ] Daniel has personally reviewed all M3 migrations.

Follow-ups (not M3):

- photos and documents, medical records;
- jurisdiction and compliance, adoption and foster workflows;
- global search across modules, and language-specific full-text search (ADR 0014);
- merging people, deleting or anonymizing people, consent records (Law 25 milestone);
- foster homes as locations, breed and colour lists, custom animal-number formats;
- correcting movements other than the latest.

Next: M4 (to be defined after pilot discovery, product-spec §41.1).
