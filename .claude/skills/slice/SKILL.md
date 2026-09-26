---
name: slice
description: Scaffold one vertical feature slice (endpoint, handler, validator, persistence, permission, tests, fr/en strings, UI route) following repo conventions. Use when implementing a single feature from an issue.
---

# /slice — one vertical feature

Input: module name, feature name, and the issue (acceptance criteria).

## 1. Read (only these)

- `backend/CLAUDE.md`, `apps/admin/CLAUDE.md`
- `docs/modules/<module>.md`
- Doc sections cited by the issue
- One existing feature in the same module as a pattern reference (if any)

Do not explore other modules. If you need another module's data, use its `Contracts/` only.

## 2. Plan (wait for approval)

List:
- Files to create/change (backend + frontend)
- Permission(s) used or added
- Migration needed? New tables → `/tenancy-check` applies
- Tests to write (names + what they prove)
- i18n keys (fr-CA / en-CA)
- Anything in the acceptance criteria you think is ambiguous

## 3. Tests first

- Domain rule tests.
- Integration test: happy path.
- Integration test: **cross-tenant denial** (tenant B cannot read/write tenant A's record).
- Integration test: **permission denied** without the required permission.

## 4. Backend

- `Modules/<Module>/Features/<Feature>/`: endpoint, request/response, handler, validator.
- Permission constant in `Authorization/`, declared on the endpoint.
- Persistence config + migration if needed (RLS policy, `TenantId`-leading indexes).
- Personal fields classified for audit.
- Timeline event + movement in the same transaction if the feature changes animal state.

## 5. Frontend

- `pnpm api:generate`.
- Route/components under `src/features/<module>/`.
- Strings in `i18n/fr-CA.json` first, then `en-CA.json`. Uncertain French → `TODO(fr-review)`.
- Use generated hooks; RHF + Zod for forms.

## 6. Verify

Run: `dotnet build backend`, `dotnet test backend`, `pnpm lint`, `pnpm test`, `pnpm api:generate` (no diff). Report results. Do not claim done if anything fails.

Then suggest `/tenancy-check` (if persistence changed) and `/close-task`.
