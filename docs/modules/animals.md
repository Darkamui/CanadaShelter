# Module: Animals

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- **Species** (M1-7, ADR 0017): global `species` (seeded, read-only at runtime), plus tenant `species_override` (relabel, hide, add).
- **Animal records** (M3-4):
  - `Animal`: per-tenant number, descriptive fields, marks and alerts, and the custody summary.
  - `AnimalIdentifier`: microchip, licence or external number.
  - `AnimalNumberCounter`: one row per tenant.
- **Timeline** (M3-4, architecture §7.1): append-only `timeline_event`, the readable history staff see.
- PostgreSQL schema: `animals`.

## Does not own

- Movements and the rules for changing custody belong to Movements. Animals only stores the summary.
- Location names come from Operations (`ILocationDirectory`); person names come from People (`IPersonDirectory`).

## Contracts (what other modules may use; ADR 0021, all scoped, stage and never save)

- `IAnimalCustody`:
  - `LockAsync` (`SELECT … FOR UPDATE`, returns the summary, or null in another tenant);
  - `Apply(expected, next)`, a compare-and-set that throws `AnimalCustodyConflictException`.
  - Lock before editing the animal in the same unit of work.
- `IAnimalTimeline.Append(TimelineEntry)`: parameter keys are identifiers, and values are GUIDs or short lowercase codes. Anything else throws.
- `IAnimalPopulation.CountAtAsync(locationIds)`: animals in care per location.

## Operational views (M3-6)

- `GET /api/animals/population` (`GetAnimalPopulation`): animals in care per location, with `count` (at the location) and `subtreeCount` (at it or below). Only locations with a non-zero subtree count are returned. Ancestors come from `ILocationDirectory.GetAsync`, walked upward with a cycle guard.
- `GET /api/animals?locationId=…&status=in_care`: the animals at a location subtree (the existing list filter).
- Both feed the location page in Operations' UI.

## Events

- None (no bus yet, ADR 0021).

## Invariants

- Numbers are gapless per tenant: the counter upsert runs in the request transaction. Tested by `Concurrent_creations_get_sequential_numbers_without_gaps`.
- An active microchip is unique per tenant: a partial unique index plus a 23505 catch. Licences and deactivated chips can repeat.
- The animal endpoints never write the custody summary; only `IAnimalCustody` does, called by Movements when it records or voids a movement (M3-5). The summary is a projection of the movement ledger.
- Edits are optimistic (`xmin` as `version`). A stale edit gets 409 with `code: animal.versionConflict`.
- The runtime role has SELECT/INSERT only on `timeline_event`. Other tables have no DELETE.
- Species input is validated against the merged visible list. A hidden species is kept on an existing animal and displays as its code.
- The timeline drops `*PersonId` parameters and person names unless the caller has `person.read`.

## Privacy notes

- Every field is classified non-personal: an animal has no audit subject to shred for.
- **Accepted risk:** marks and the three alerts are free text that staff could fill with personal data. The UI warns, but nothing enforces it. The audit pipeline must not copy these values (record "changed" only).
- The timeline holds IDs and codes only, so there is nothing to correct or shred there.

## Permissions

- `animal.read`: list, get, timeline, species, population.
- `animal.write`: create, update, add and deactivate identifiers.

## Key files

- `Domain/Animal.cs`, `AnimalIdentifier.cs`, `TimelineEvent.cs` (plus the counter), `Species.cs`.
- `Persistence/AnimalsModelContributor.cs`: tables, the trigram index on `search_text`, and the partial microchip index.
- `Features/Animals/AnimalEndpoints.cs`: `/api/animals` (`ListAnimals`, `GetAnimal`, `CreateAnimal`, `UpdateAnimal`, `AddAnimalIdentifier`, `DeactivateAnimalIdentifier`, `GetAnimalTimeline`, `GetAnimalPopulation`).
- `Features/Animals/AnimalContracts.cs`: the contract implementations. `AnimalRequestValidator.cs`. `Features/ReferenceData/SpeciesCatalog.cs`.
- Frontend: `apps/admin/src/features/animals/` (list, create and detail pages, `IdentifiersPanel`, `TimelinePanel`). The detail page hosts Movements' `MovementsPanel`; the create page can open the intake dialog next (`startIntake`).

## Open questions / TODO

- Endpoints to manage species overrides.
- Photos, medical records, breed and colour lists, and custom number formats (M3 follow-ups).
