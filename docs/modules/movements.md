# Module: Movements

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- The intake reason reference list (M1-7, ADR 0017):
  - `IntakeReason` is global: seeded by migration and read-only at runtime.
  - `IntakeReasonOverride` is tenant-owned and uses RLS. A tenant can relabel, hide or add a reason.
- Target (architecture §6): intake, transfer and outcome movements (the movement ledger, architecture §7.1). None of this exists yet (M3).
- PostgreSQL schema: `movements`.

## Does not own

- Species belongs to Animals.
- `LocalizedText`, `MapLocalizedText` and `ReferenceList` are in `BuildingBlocks/Localization`.

## Contracts (what other modules may use)

- None yet. Other modules store the intake reason `code`.

## Events

- None yet.

## Invariants

- The runtime role can only SELECT system reasons, and each tenant's overrides affect that tenant only. Tested by `ReferenceDataEndpointTests`.

## Permissions

- `movement.read` (read, non-sensitive): `GET /api/movements/intake-reasons`.

## Key files

- `Domain/IntakeReason.cs`: `IntakeReason` and `IntakeReasonOverride`.
- `Persistence/MovementsModelContributor.cs`: tables `intake_reason` and `intake_reason_override`.
- `Features/ReferenceData/ListIntakeReasonsEndpoint.cs`: `GET /api/movements/intake-reasons` (`ListMovementsIntakeReasons`).

## Open questions / TODO

- The seeded French labels need a language check. `owner_surrender`, `transfer_in` and `born_in_care` are marked `TODO(fr-review)` in migration `AddReferenceData`.
- **M2:** permission to read the list, and endpoints to manage overrides.
