# backend/CLAUDE.md

.NET 10, ASP.NET Core, EF Core, PostgreSQL, Hangfire (PostgreSQL storage), xUnit.

## Layout

```text
Shelter.Host/            Program.cs, middleware, composition only. No business logic.
BuildingBlocks/Shelter.BuildingBlocks/
                         One project; a folder per concern (Tenancy, Persistence, Authorization,
                         Auditing, Jobs, Documents, Communications, Integrations) as each arrives
Modules/<Module>/
  Shelter.Modules.<Module>/            Implementation (internal by default)
  Shelter.Modules.<Module>.Contracts/  The ONLY assembly other modules may reference

Inside Shelter.Modules.<Module>/:
  Domain/                Entities, value objects, domain rules
  Features/<Feature>/    Endpoint + request/response + handler + validator (colocated)
  Persistence/           EF configurations, module schema
  Authorization/         Permission constants + policies
Tests/Shelter.IntegrationTests/  Host pipeline tests (Testcontainers PostgreSQL, one container per run)
Tests: sibling test project per module (see ADR 0015, written in M0-8)
ArchitectureTests/       Enforces module boundaries (Rules/: pure rules + real solution graph)
```

**Boundary rules** (enforced by `Shelter.ArchitectureTests`, over declared `ProjectReference`s and compiled assembly references):

- A module references another module only through its `.Contracts` project; never the host.
- `.Contracts` projects reference only `BuildingBlocks` and other `.Contracts`.
- `BuildingBlocks` references no module, contracts, or host project.
- `Shelter.Host` declares types only in `Shelter.Host`, `.Composition`, `.Middleware` (plus `Program`).
- A new project must follow the naming convention (`Shelter.Modules.<Module>[.Contracts]`, `Shelter.BuildingBlocks[.*]`) or the rules won't see it.

## Conventions

- **Module registration:** each module has one public `<Module>Module : IModule` (`BuildingBlocks/Modules`). The Host lists every module explicitly in `Shelter.Host/Composition/ModuleCatalog.cs` and maps it under `/api/{RoutePrefix}` (also the OpenAPI tag). No assembly scanning.
- **Endpoints:** Minimal APIs, grouped per module, one file per feature. Every endpoint declares a permission. No anonymous endpoints outside the public route group.
- **Handlers:** plain classes injected directly. No MediatR.
- **Validation:** validator per request, errors returned as RFC 7807 ProblemDetails.
- **Persistence:** follow the DbContext ADR (M0). Each module owns its own PostgreSQL schema. Cross-module foreign keys are by ID only, no navigation properties across modules.
- **Naming:** snake_case in the database (naming-convention package), PascalCase in C#.
- **IDs:** GUID v7 (`Guid.CreateVersion7()`).
- **Time:** store UTC (`DateTimeOffset`); render in the organization's timezone at the edge.
- **Tenancy:** tenant-owned entities implement `ITenantOwned`. `TenantId` is set by the persistence interceptor, never by handlers or request bodies.
- **Bilingual data:** use the `LocalizedText` value object (`Fr`, `En`) mapped to two columns.
- **Personal data:** mark personal fields via the audit classification (see `BuildingBlocks/Auditing`). Unclassified fields on a Person-linked entity fail the architecture test.
- **Transactions:** a state change, its movement row, and its timeline event are written in one transaction (architecture §7.1).
- **Errors:** domain failures return results/ProblemDetails. Exceptions are for bugs.
- **Logging:** structured (JSON console, scopes carry `CorrelationId`/`RequestId`/`TraceId`); never log personal field values. Use source-generated `[LoggerMessage]` and mark parameters `[PersonalData]` (erased) or `[NonPersonalData]` (`BuildingBlocks/Logging`).
- **OpenAPI:** give every endpoint `.WithName("<Verb><Module><Thing>")` (becomes the Orval hook name). `dotnet build backend/Shelter.Host -p:ExportOpenApi=true` writes `packages/api-client/openapi.json` (also on Release/CI builds). Commit it.

## Migrations

- One migration per issue where possible, named after the change.
- Every new tenant-owned table's migration includes: `ENABLE` + `FORCE ROW LEVEL SECURITY`, the tenant policy (via the policy helper), and `TenantId`-leading indexes.
- Never modify a migration that exists on `main`.

## Tests

- xUnit v3 on Microsoft.Testing.Platform (opted in via root `global.json`). Test projects reference `xunit.v3` (never `Microsoft.NET.Test.Sdk`/`xunit.runner.visualstudio`) and add `<Using Include="Xunit" />`. Host-level tests use `ShelterApiFactory` + the assembly-wide `PostgresFixture` (mounts `infrastructure/docker/postgres/init/01-roles.sh`).
- Build settings: `Directory.Build.props` (nullable, warnings as errors, analyzers) and central package versions in `Directory.Packages.props`. Never put a `Version` on a `PackageReference`.
- Unit tests for domain rules.
- Integration tests with Testcontainers PostgreSQL, running as the **runtime role** (not the migration role) so RLS is actually exercised.
- Every feature touching tenant data has at least one cross-tenant denial test.
- Every endpoint has at least one permission-denied test.
