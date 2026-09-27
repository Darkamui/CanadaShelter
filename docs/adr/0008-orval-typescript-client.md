# 0008. Orval for the TypeScript client

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

The backend is the source of truth for contracts (§2.2, §4.2). The admin app uses TanStack Query and Zod. Hand-written fetch code drifts from the API, and nobody notices until runtime.

## Decision

- **Pipeline:**
  - `Microsoft.Extensions.ApiDescription.Server` exports the Host's OpenAPI document at build time to `packages/api-client/openapi.json`. Export happens on Release builds or with `-p:ExportOpenApi=true`.
  - **Orval** (`packages/api-client/orval.config.ts`) generates two outputs:
    - TanStack Query hooks + models (`src/generated/hooks`, `src/generated/model`), split by OpenAPI tag (= module), using the fetch client.
    - Zod schemas (`src/generated/zod`).
  - Prettier formats the output, so regeneration is deterministic.
- **Mutator:** all requests go through the hand-written mutator `src/http/fetcher.ts` (`shelterFetch`). It sends `credentials: 'same-origin'` (ADR 0007) and throws a typed `ApiError` that carries the RFC 7807 ProblemDetails.
- **Committed output:** both `openapi.json` and `src/generated/` are committed, and generated code is never edited by hand.
- **How to regenerate:** `pnpm api:generate`. The CI job `api-sync` regenerates and fails on any diff.
- **Naming:** the operationId comes from `.WithName("<Verb><Module><Thing>")`, e.g. `GetPlatformPing` → `useGetPlatformPing`.

## Alternatives considered

- **openapi-typescript + openapi-fetch**: lighter, but no Query hooks or Zod, so we'd hand-write that glue for every endpoint.
- **NSwag TypeScript client**: class-based output with no TanStack Query integration, and the generator runs in .NET.
- **Hand-written client**: drifts from the API, and there's no compile-time signal when a contract changes.
- **Not committing generated output** (generate in CI/postinstall): reviewers can't see contract changes in PR diffs, and the frontend build would depend on the .NET SDK.

## Consequences

- Positive:
  - A contract change shows up as a TypeScript compile error and a visible diff.
  - Consistent error handling through `ApiError`.
- Negative / accepted trade-offs:
  - Every API change needs `pnpm api:generate` plus a commit of the regenerated files.
  - Generated diffs add noise to PRs.
  - Upgrading Orval can churn the output, so bump it deliberately.
- Follow-ups: when auth lands (M2), the mutator gains CSRF header handling.
