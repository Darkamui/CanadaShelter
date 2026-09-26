# Module: <Name>

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- Entities / concepts this module is the source of truth for.
- PostgreSQL schema: `<schema>`

## Does not own

- Things people might assume are here but live elsewhere (and where).

## Contracts (what other modules may use)

- `Contracts/...` types and what they are for.

## Events

- Emits: `<Event>` — when, payload IDs.
- Consumes: `<Event>` from `<Module>` — what it does.

## Invariants

- Rules that must always hold (and the test that proves each).

## Permissions

- `<permission>` — what it allows.

## Key files

- `Features/<Feature>/` — one line each.

## Open questions / TODO

- Known gaps, deferred items (with issue links).
