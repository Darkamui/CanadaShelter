# 0005. DbContext strategy: single composed DbContext, schema per module

- **Status:** Proposed (owner: Daniel; must be Accepted before M1 starts)
- **Date:** 2026-09-26

## Context

Each module owns its PostgreSQL schema and persistence configuration (ADR 0001, §5.3). Three forces pull against a strict "one context per module" layout:

- **Same-transaction writes (§7.1):** a state change, its movement row and its timeline event are written in one transaction, and these rows belong to different modules.
- **`SET LOCAL` is per transaction (ADR 0004):** the tenant has to be set once, inside the transaction every participating write uses.
- **Cross-cutting interceptors:** tenant stamping, tenant query filters, audit capture with crypto-shredding (§17.3) and timestamps must behave identically for every entity.

This matters in M1: the tenant interceptor, the audit interceptor and the first migrations are all built on whichever layout we pick.

## Decision (recommended)

- **One composed context.** A single `ShelterDbContext` in BuildingBlocks/Persistence, built from **module-owned configurations**:
  - Each module keeps its `IEntityTypeConfiguration<T>` classes `internal` in `Modules/<X>/Persistence/`.
  - Each module registers an `IModelConfiguration` (or similar) through its `IModule.AddServices`, which applies those configurations and sets the module's schema (`animals`, `people`, `platform`, …).
  - The context loops over the registered contributors in `OnModelCreating`, so it never references module types.
- **Transactions and tenant scope:** one transaction per unit of work, so a single `SET LOCAL` covers every module's writes.
- **Interceptors and filters:** the tenant, audit and timestamp interceptors, plus the `ITenantOwned` query filter, are registered once.
- **Migrations:**
  - One migrations assembly and one history table, `platform.__ef_migrations_history`.
  - Migrations run as `shelter_migrator` through `scripts/db-migrate-local.sh` locally.
- **Boundaries stay enforced:**
  - Handlers use `Set<T>()` for their **own** module's entities through a module-local accessor.
  - There are no navigation properties across modules; foreign keys are by ID only.
  - An architecture test forbids referencing another module's entity types. Those types are `internal`, and the ADR 0015 rules already block the project reference.

## Alternatives considered

- **Per-module DbContexts sharing one `NpgsqlConnection` and transaction** (`UseTransaction` / `Database.SetDbConnection`).
  - Pros:
    - Strongest compile-time isolation.
    - Separate migration histories let one module's migrations evolve without touching the others.
  - Cons:
    - Every cross-module unit of work has to coordinate the shared connection and enlist each context in the transaction, and a missed enlistment silently runs outside the tenant scope.
    - Interceptors and filters are registered nine times, and must be kept identical.
    - Nine history tables, plus ordering problems when module migrations depend on shared functions (e.g. the RLS helper).
    - SaveChanges ordering across contexts is manual.
- **Per-module DbContexts with separate transactions plus an outbox** between modules. This breaks §7.1's atomicity requirement.
- **One context with no module split** (a single DbSet list in one file). Simplest to write, but module ownership of persistence erodes, and ADR 0001 exists to prevent exactly that.

## Consequences

- Positive:
  - §7.1 atomicity and `SET LOCAL` scoping come for free.
  - One interceptor pipeline, one migration history, and simple M1 plumbing.
- Negative / accepted trade-offs:
  - Every migration is generated against the whole model, so two branches that both add migrations conflict on the model snapshot and must be rebased.
  - Boundary isolation relies on `internal` types plus architecture tests rather than separate contexts.
  - A module can't be pulled out without splitting its migrations first.
- Follow-ups:
  - M1-1: implement the composed context, schemas and the history table location.
  - Add an architecture rule: no public entity types in module assemblies.
  - Record any deviation here before building on it.
