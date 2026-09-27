# Module: Animals

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- The species reference list (M1-7, ADR 0017):
  - `Species` is global: seeded by migration and read-only at runtime.
  - `SpeciesOverride` is tenant-owned and uses RLS. A tenant can relabel, hide or add a species.
- Target (architecture §6): animal records, medical and behaviour data. None of this exists yet (M3).
- PostgreSQL schema: `animals`.

## Does not own

- Intake reasons and animal movements belong to Movements.
- `LocalizedText`, `MapLocalizedText` and `ReferenceList` are in `BuildingBlocks/Localization`.

## Contracts (what other modules may use)

- None yet. Other modules store the species `code` (a stable, language-neutral string).

## Events

- None yet.

## Invariants

- The runtime role can only SELECT system species. Each tenant's overrides affect that tenant only. Tested by `ReferenceDataEndpointTests`.
- Both labels (`label_fr`, `label_en`) are always present.

## Permissions

- `animal.read` (read, non-sensitive): `GET /api/animals/species`.

## Key files

- `Domain/Species.cs`: `Species` and `SpeciesOverride`.
- `Persistence/AnimalsModelContributor.cs`: tables `species` and `species_override`.
- `Features/ReferenceData/ListSpeciesEndpoint.cs`: `GET /api/animals/species` (`ListAnimalsSpecies`) → `[{ code, label: { fr, en } }]`.
- Frontend: `apps/admin/src/features/animals/` (the `SpeciesList` component on the Animals page).

## Open questions / TODO

- **M2:** permission to read the list, and endpoints to manage overrides.
- **M3:** animals store the species code and validate it against the merged list.
