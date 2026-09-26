# Module: Platform

> A map for future sessions. Keep under ~80 lines. Update via `/close-task`.

## Owns

- Target ownership (architecture §6.9): tenant provisioning, authentication integration, staff memberships, external identities, permissions, privacy workflows, consent, retention, audit access, integrations, webhooks, imports/exports, feature flags, SaaS administration.
- **Today (M0):** only `GET /api/platform/ping`. No entities, no schema yet.
- PostgreSQL schema: `platform` (arrives with the first Platform table in M1; the DbContext strategy is ADR 0005).

## Does not own

- Host plumbing (health checks, ProblemDetails, correlation IDs, JSON logging, OpenAPI) lives in `backend/Shelter.Host/Composition` and `Middleware`.
- The data classification taxonomy and log redaction live in `BuildingBlocks/Logging` (shared by every module).

## Contracts (what other modules may use)

- None yet (`Shelter.Modules.Platform.Contracts` is empty).

## Events

- None yet.

## Invariants

- The request pipeline works end to end: `HostPipelineTests.Ping_returns_ok` (in `backend/Tests/Shelter.IntegrationTests`).

## Permissions

- None yet. `GET /api/platform/ping` is explicitly `.AllowAnonymous()` until identity exists (M2).

## Key files

- `PlatformModule.cs`: the module's `IModule` registration entry point (route prefix `platform`).
- `Features/Ping/PingEndpoint.cs`: `GET /api/platform/ping` → `{ "status": "ok" }`, operationId `GetPlatformPing`.

## Open questions / TODO

- **M2:** set an authorization `FallbackPolicy` so default-deny is structural (CLAUDE.md hard rule 3). Then give ping a permission or move it to the public route group.
