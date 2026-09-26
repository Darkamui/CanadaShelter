# 0015. Backend project and test layout

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

ADR 0001 needs boundaries that tooling can check, and a module needs a public surface that other modules may depend on without seeing its internals (§5.3). Tests need a consistent home, and must run against real PostgreSQL as the runtime role (CLAUDE.md, §27). This ADR records the layout built in M0.

## Decision

- **Projects** (listed in `backend/Shelter.slnx`):
  - `Shelter.Host`: composition only. Types may live only in the `Shelter.Host`, `.Composition` and `.Middleware` namespaces.
  - `Shelter.BuildingBlocks`: one project, with a folder per concern.
  - Per module, `Modules/<X>/Shelter.Modules.<X>` (implementation, `internal` by default, one public `<X>Module : IModule`) and `Shelter.Modules.<X>.Contracts` (the only assembly other modules may reference).
- **Composition:** the Host lists modules explicitly in `Composition/ModuleCatalog.cs`, with no assembly scanning, and maps each under `/api/{RoutePrefix}`.
- **Boundary rules:** `ArchitectureTests/Shelter.ArchitectureTests` runs pure rules (`Rules/BoundaryRules.cs`) over the union of declared `ProjectReference`s and compiled assembly references. It also includes deliberately failing synthetic cases. Project names are how it classifies projects, so new projects must follow the naming convention.
- **Tests:**
  - xUnit v3 on Microsoft.Testing.Platform (root `global.json`).
  - `Tests/Shelter.IntegrationTests`: tests that go through the Host pipeline (`ShelterApiFactory` + assembly-wide Testcontainers `PostgresFixture`, using the same init script as local Docker, connected as `shelter_app`).
  - Module tests: a sibling `Modules/<X>/Shelter.Modules.<X>.Tests`, created with the module's first test. The boundary rules count it as part of module X, so it may reference its own module, Contracts and BuildingBlocks, but not the Host or other modules' internals. Anything that needs HTTP goes in `Tests/Shelter.IntegrationTests`.

## Alternatives considered

- **Single project per module with `internal` + `InternalsVisibleTo`**: no separate public contract surface, and the boundary is enforced only by accessibility, which is easy to widen.
- **Domain/Application/Infrastructure projects per module**: 27+ projects for nine modules, and the ceremony isn't justified at this size.
- **NetArchTest / ArchUnitNET**: extra dependency, and it doesn't see declared-but-unused project references. Plain reflection plus csproj parsing covers both.
- **One test project for everything**: slow feedback, and module tests could reach into every module.

## Consequences

- Positive:
  - Boundaries are checked on every `dotnet test`.
  - A clear home for each kind of test.
- Negative / accepted trade-offs:
  - Two projects per module even while Contracts is empty.
  - The naming convention is load-bearing.
  - Module tests can't use `ShelterApiFactory` directly.
- Follow-ups: when the first module test project needs PostgreSQL, extract the fixture into a shared `Tests/Shelter.Testing` support project.
