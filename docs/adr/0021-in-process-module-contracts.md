# 0021. In-process module contracts

- **Status:** Accepted
- **Date:** 2026-10-03

## Context

From M3 on, modules depend on each other.

- Movements must change an animal's custody summary and append to its timeline.
- Animals must show location and person names.
- Operations must refuse to archive a location that still holds animals.

Hard rule 7 forbids touching another module's tables. Architecture §7.1 requires a movement, the custody summary and the timeline event to commit together. ADR 0005 already composes every module into one `ShelterDbContext`, and the UnitOfWork wraps each write request in one transaction with `SET LOCAL` tenancy (architecture §8).

## Decision

- **Contracts project.** A module's public interfaces and DTOs live in its `*.Contracts` project, for example `IAnimalCustody`, `IAnimalTimeline`, `IAnimalPopulation`, `ILocationDirectory` and `IPersonDirectory`. Other modules reference only that project, and architecture tests enforce it.
- **Implementations stay internal.** They live in the owning module and are registered as scoped services in its `AddServices`.
- **Same scope, same transaction.** The caller resolves a contract from its own request scope. The contract therefore uses the caller's `ShelterDbContext`, so its work joins the caller's UnitOfWork transaction and tenant context.
- **Stage, never save.** Contract methods that write only stage changes. They never call `SaveChanges` or open a connection, and the caller saves once. Writes that must not race take a row lock first (`IAnimalCustody.LockAsync` runs `SELECT … FOR UPDATE`).
- **The owner keeps its rules.** The owning module validates what it is handed. For example, `IAnimalCustody.Apply` checks the expected-to-next summary, and `IAnimalTimeline.Append` accepts only IDs and codes.
- **No event bus or outbox yet.** They wait until a consumer needs asynchronous or retryable work, such as notifications or search indexing.

## Alternatives considered

- **Domain events published in process, with handlers in other modules.** Looser coupling, but the caller can't see a handler's failure, ordering is implicit, and this team doesn't need that indirection yet.
- **An outbox and async handlers.** These give eventual consistency, which breaks §7.1's "summary and timeline commit with the movement", and they add infrastructure (rule 8).
- **Reading another module's tables directly, or through a shared view.** Breaks rule 7 and makes schemas impossible to change on their own.
- **Each module with its own DbContext and transaction.** It needs distributed or two-phase coordination for one request, which contradicts ADR 0005.

## Consequences

- Positive:
  - Cross-module writes are atomic, with no new infrastructure.
  - RLS and `SET LOCAL` apply to contract work for free.
  - Contracts are plain interfaces, easy to fake in module tests.
- Negative / accepted trade-offs:
  - Modules are coupled at run time. They are deployed together, and an implementation's slow query slows the caller's request.
  - The caller's transaction gets longer and holds row locks across modules.
  - Contracts depend on the caller to save. A contract call without a following save is silently lost, and only tests catch it.
  - Splitting a module into a service later means replacing these calls with messages.
- Follow-ups:
  - Add an outbox (and an ADR) when the first asynchronous consumer appears.
