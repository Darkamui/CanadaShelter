# Module: People

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- `Person`: an individual or organization the shelter deals with (owner, adopter, foster, volunteer, donor, finder, surrenderer). Contact details, address, preferred language, role tags, notes, archived flag.
- PostgreSQL schema: `people` (table `person`).

## Does not own

- Staff users and memberships (Platform). A staff member is not a `Person`.
- Consent records, anonymization and deletion (deferred to a Law 25 milestone).
- Links between people and animals (Movements records the person on an intake or outcome by ID).

## Contracts (what other modules may use)

- `Shelter.Modules.People.Contracts/IPersonDirectory` (scoped, same `ShelterDbContext` as the caller):
  - `GetSummariesAsync(ids)` → `PersonSummary(Id, DisplayName, IsArchived)` for display.
  - `ExistsActiveAsync(id)` → validates a person picked on another module's form.

## Events

- None.

## Invariants

- Every column except the ID, tenant, archived flag and timestamps is personal data, including the normalized search columns (`search_text`, `normalized_email`, `phone_digits`, `secondary_phone_digits`). The audit subject is the person's own ID (`PeopleModelContributor`; `ClassificationRuleTests`, `PeopleEndpointTests.Audit_*`).
- A person needs a first name, last name or display name. The display name defaults to "first last" (`PersonTests`).
- Preferred language is `fr` or `en` (same codes as Platform users, not `fr-CA`), default `fr`.
- No hard delete: the runtime role has SELECT/INSERT/UPDATE only. Archive hides a person from the default list and search.
- Possible duplicates (same normalized email or phone digits, within the tenant) are returned on create and never block it (`PeopleEndpointTests`, `PeopleIsolationEndpointTests`).
- Search is accent- and case-insensitive on name and email (`SearchNormalizer`), digit-based on phones (≥ 3 digits); punctuation-only queries filter nothing.
- Tenant isolation: `PeopleIsolationTests`, `PeopleIsolationEndpointTests` (cross-tenant 404, no list/search/duplicate leakage, body `tenantId` ignored).

## Permissions

- `person.read` — list, search and view people.
- `person.write` — create, edit, archive and unarchive.

## Key files

- `Domain/Person.cs` — entity, role tag codes, province codes, normalized columns.
- `Persistence/PeopleModelContributor.cs` — mapping, classification, indexes (GIN trigram on `search_text` cannot lead with `tenant_id`; RLS still filters).
- `Features/People/PeopleEndpoints.cs` — `/api/people` endpoints and DTOs.
- `Features/People/PersonRequestValidator.cs` — trimming, normalization, field errors.
- `Features/People/PersonDirectory.cs` — `IPersonDirectory` implementation.
- UI: `apps/admin/src/features/people/` (list, create, detail/edit; catalogs `people` namespace).

## Open questions / TODO

- Person merge, consent records, anonymization/deletion (Law 25 milestone).
- `person.read` shows every field, notes and address included. Revisit with pilots whether a narrower read (e.g. for volunteer coordinators) is needed (Law 25 minimization).
- `_frReview`: `roles.finder`, `roles.surrenderer`, `detail.duplicatesBody`.
