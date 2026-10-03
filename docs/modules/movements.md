# Module: Movements

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- **Reference lists** (ADR 0017 + amendment 1): intake reasons (M1-7) and outcome types (M3-5). Each has a global table (seeded, SELECT-only at runtime) and a tenant `*_override`. Both carry `sac_category` (Shelter Animals Count, `Domain/SacCategories.cs`, `TODO(pilot-review)`).
- **The movement ledger** (M3-5, architecture §7.1): append-only `movement` rows of type `intake`, `relocation`, `outcome` or `void`, and the custody rules that go with them.
- PostgreSQL schema: `movements` (`intake_reason`, `intake_reason_override`, `outcome_type`, `outcome_type_override`, `movement`).

## Does not own

- The custody summary on the animal: Animals stores it, Movements writes it through `IAnimalCustody` (ADR 0021).
- The timeline: Movements appends to it through `IAnimalTimeline`.
- Locations (`ILocationDirectory`) and people (`IPersonDirectory`).

## Contracts (what other modules may use)

- None. Other modules store the reason/outcome `code`.

## Events

- None (ADR 0021: in-process contracts, same transaction).

## Invariants

- Recording runs under the animal's row lock (`IAnimalCustody.LockAsync`, `FOR UPDATE`), then stages the movement, the new summary (`Apply(expected, next)`) and a timeline event in one unit of work: all or nothing (`MovementEndpointTests.Movement_summary_and_timeline_commit_together_or_not_at_all`). Two concurrent intakes → 201 + 409 `movement.custodyConflict`.
- Intake needs `not_in_care`, or an outcome whose category is not terminal (died, euthanized). Relocation and outcome need `in_care`.
- The target location is active and holds animals: `ILocationDirectory.LockForPlacementAsync` takes `FOR SHARE` on it, so a concurrent archive (`FOR UPDATE` + `IAnimalPopulation` count) cannot slip between.
- The person must exist and be active; adoption and return to owner (category) require one. Without `person.read` a person ID is refused (403).
- `occurredAt` defaults to now, is truncated to microseconds, is ≤ now + 5 min, and ≥ the latest movement in effect.
- Void: only the latest movement in effect, reason required, `movement.amend`. It is a new row (`voids_movement_id`, tenant-scoped FK); the summary is rebuilt from the ledger (`Ledger.Project`). If that puts the animal back at a location since archived, 409.
- The runtime role has SELECT/INSERT only on `movement` (`MovementsIsolationTests.Runtime_role_cannot_update_or_delete_a_movement`).
- Reason and outcome input is validated against the visible merged list; hidden codes keep their category.
- Tenant isolation: `MovementsIsolationTests`, `MovementIsolationEndpointTests` (including another organization's person).

## Privacy notes

- `notes` is personal, with `person_id` as the audit subject; a void copies the voided movement's person so its reason is shredded with it.
- Without `person.read`, `GET /api/movements` nulls `personId`, `personName` and `notes`.
- Timeline parameters are codes and IDs only.
- **Accepted gap:** relocations have no person, and neither does a void of one, so their notes have no audit subject and cannot be shredded by person. The UI asks for no personal information there; nothing enforces it.
- **Follow-up (privacy milestone):** notes cannot be corrected (no UPDATE grant); anonymizing will need a grant or a job.
- `person_id` (here and in timeline parameters) is classified non-personal: it is an opaque ID, gated behind `person.read` on every read (`MovementPersonPrivacyTests`). When a person is erased the ID stays and resolves to nothing; readers already tolerate a missing person.

## Permissions

- `movement.read`: history, intake reasons, outcome types.
- `movement.write`: record an intake, relocation or outcome.
- `movement.amend` (sensitive, administrator only): void.

## Key files

- `Domain/Movement.cs`, `OutcomeType.cs`, `IntakeReason.cs`, `SacCategories.cs`.
- `Features/Movements/MovementEndpoints.cs`: `/api/movements` (`ListMovements`, `RecordIntake`, `RecordRelocation`, `RecordOutcome`, `VoidMovement`).
- `Features/Movements/MovementRecorder.cs`: the rules. `Ledger.cs`: in-effect order and the projected summary.
- `Features/ReferenceData/ReasonCatalog.cs`: merged lists; `ListOutcomeTypesEndpoint.cs` (`ListMovementsOutcomeTypes`).
- Migration `AddMovementLedger`.
- Frontend: `apps/admin/src/features/movements/` (`MovementsPanel` on the animal page, `MovementDialog`, `PersonPicker`).

## Open questions / TODO

- Correcting movements older than the latest one in effect (M3 follow-up).
- Endpoints to manage reason and outcome overrides.
- `TODO(fr-review)`: outcome labels `return_to_owner`, `transfer_out`, `return_to_field` (migration); intake reasons from `AddReferenceData`. `TODO(pilot-review)`: SAC mapping of system reasons.
- Existing tenant reason overrides got `other_intake` in the migration (RLS hides them from the migrator); tenants must recategorize.
