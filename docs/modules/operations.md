# Module: Operations

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- `Location`: the organization's location tree (shelter → building → room → kennel, plus external places such as a partner clinic or the field). Parent, kind, plain-text name, optional capacity, archived flag.
- Location kind list (ADR 0017 + amendment 1): global `location_kind` (seeded, SELECT-only for the runtime role) and tenant `location_kind_override`. Each kind has a `holds_animals` attribute.
- PostgreSQL schema: `operations` (tables `location`, `location_kind`, `location_kind_override`).

## Does not own

- Where an animal is (Movements records it; Animals keeps the custody summary). Archiving a location with animals in care is blocked (`IAnimalPopulation`, M3-5).
- Foster homes as locations (follow-up).
- Tasks, schedules, kennel cards (later milestones).

## Contracts (what other modules may use)

- `Shelter.Modules.Operations.Contracts/ILocationDirectory` (scoped, same `ShelterDbContext` as the caller):
  - `GetAsync(ids)` → `LocationSummary(Id, ParentId, Name, KindCode, IsArchived)` for display.
  - `GetSubtreeIdsAsync(rootId)` → the root and every location below it, archived included (filters "animals in this building").
  - `LockForPlacementAsync(id)` → the location exists, is active and its kind holds animals; takes `FOR SHARE` on the row so a concurrent archive waits (validates a movement's target, M3-5).
- `OperationsPermissionNames.LocationRead`: for modules that check `location.read` in a handler (Animals' population, M3-6).

## Events

- None.

## Invariants

- Every write takes a per-tenant `pg_advisory_xact_lock` first (`LocationTree.LockAsync`), so concurrent moves cannot form a cycle and concurrent creates cannot both pass the name check.
- A move never puts a location inside itself or a descendant (recursive ancestor check; `LocationEndpointTests.Update_renames_and_moves_but_never_inside_itself`).
- Names are unique among active siblings, ignoring accents and case (`normalized_name`; partial unique index `NULLS NOT DISTINCT WHERE NOT is_archived`, so top-level locations are siblings).
- The parent must exist in the organization and be active. The parent foreign key includes `tenant_id`, because a plain key check bypasses RLS (`LocationIsolationEndpointTests`).
- A new kind must be a visible kind of the merged list. A hidden kind stays valid on locations that already have it, and keeps its `holds_animals` (`OperationsIsolationTests.Hidden_kind_keeps_its_holds_animals_attribute`).
- Archive is blocked (409, `code = location.hasActiveChildren`) while active children remain, and (409, `code = location.hasAnimals`) while animals are in care there. Archive locks the row `FOR UPDATE` before counting, against `LockForPlacementAsync`'s `FOR SHARE` (`MovementEndpointTests.A_location_with_animals_in_care_cannot_be_archived`). No unarchive yet; no hard delete (runtime role has SELECT/INSERT/UPDATE only).
- No personal data: names are places, all columns are classified non-personal.
- Tenant isolation: `OperationsIsolationTests` (location, override, kind catalog, directory), `LocationIsolationEndpointTests` (cross-tenant 404/400, body `tenantId` ignored, `read_only` 403 on writes).

## Permissions

- `location.read` — view the tree and the kind list.
- `location.write` — create, rename, move and archive.

## Key files

- `Domain/Location.cs`, `Domain/LocationKind.cs` — entities.
- `Persistence/OperationsModelContributor.cs` — mapping, tenant-scoped self foreign key, indexes.
- `Features/Locations/LocationEndpoints.cs` — `/api/operations/locations`, `/api/operations/location-kinds`, validation, DTOs.
- `Features/Locations/LocationTree.cs` — advisory lock and recursive CTEs.
- `Features/Locations/LocationKindCatalog.cs` — merged kind list (hidden included).
- `Features/Locations/LocationDirectory.cs` — `ILocationDirectory` implementation.
- UI: `apps/admin/src/features/locations/` (catalog `locations` namespace):
  - `LocationsPage`: the tree at `/operations`, with the create/edit dialog. Each name links to its page; with `animal.read` each active location shows its animals in care, the locations below included.
  - `LocationDetailPage` (`/operations/locations/:locationId`, M3-6): breadcrumb, kind and capacity, counts here and below, the active locations inside, and a paged table of the animals in care in the subtree.
  - `usePopulation`: reads Animals' `GET /api/animals/population` only when the user has `animal.read`.

## Open questions / TODO

- Location names are classified non-personal. Foster homes as locations (a name like a caregiver's) will need a personal classification and an audit subject first.
- Unarchive, and endpoints to manage kind overrides (ADR 0017 follow-up).
- Not guarded: changing a location's kind to one that does not hold animals while animals are there (M3 follow-up).
- `shelter` kind holds animals so a small rescue can use a single location; revisit with pilots.
- `TODO(fr-review)` in the migration: Enclos, Isolement, Clinique vétérinaire externe, Terrain. `_frReview`: `archiveBlocked`.
