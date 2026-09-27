# CLAUDE.md

Québec-first, bilingual shelter & rescue management SaaS. Multi-tenant. ASP.NET Core modular monolith + PostgreSQL + React/Vite PWA.

**Current phase:** M0/M1 (foundations). Pilot features (adoption, payments, compliance pack, importer) are **gated** on pilot discovery (product-spec §41.1). Do not start them unless an issue explicitly says so.

## Repo map

- `backend/` — .NET solution. `Shelter.Host`, `Modules/*`, `BuildingBlocks/*`, `ArchitectureTests`. See `backend/CLAUDE.md`.
- `apps/admin/` — React/Vite staff app (also hosts public routes in V1). See `apps/admin/CLAUDE.md`.
- `packages/api-client/` — **generated** by Orval. Never edit by hand.
- `packages/ui/` — shared shadcn/ui components.
- `packages/adoption-widget/` — embeddable public widget.
- `packages/config/` — shared ESLint (flat), Prettier, and tsconfig presets.
- `infrastructure/` — Docker Compose, OpenTofu.
- `global.json` (repo root) — pins the .NET 10 SDK and opts `dotnet test` into Microsoft.Testing.Platform.
- `docs/` — specs, ADRs, module docs, issues.

## Commands

- `docker compose -f infrastructure/docker/compose.yml up -d` — Postgres, MinIO, Mailpit
- `dotnet build backend` / `dotnet test backend`
- `pnpm install` / `pnpm dev` / `pnpm test` / `pnpm lint` / `pnpm format:check`
- `pnpm test:e2e` — Playwright E2E (first run: `pnpm --filter @shelter/admin exec playwright install chromium`)
- pnpm comes from corepack (`packageManager` in root `package.json`). If `corepack enable` fails with EPERM on Windows, use `corepack enable --install-directory "$APPDATA/npm" pnpm`.
- `pnpm api:generate` — regenerate Orval client after any API contract change
- `./scripts/db-migrate-local.sh` — apply migrations to the **local** database only (as `shelter_migrator`)
- Local DB created before M1? Reset once for the `shelter_platform_admin` role: `docker compose -f infrastructure/docker/compose.yml down -v` then `up -d`

(Keep this list accurate. If a command changes, update it in the same PR.)

## Where to look (read sections, not whole files)

Specs are long. Grep for the heading and read only the named section.

| Topic                                   | Location                                                         |
| --------------------------------------- | ---------------------------------------------------------------- |
| Module ownership                        | architecture §6                                                  |
| Three histories (ledger/timeline/audit) | architecture §7.1                                                |
| Tenancy, RLS, `SET LOCAL`               | architecture §8                                                  |
| Auth, staff vs external users           | architecture §9                                                  |
| Rule packs / Québec rules               | architecture §10                                                 |
| Bilingual data                          | architecture §11                                                 |
| Search                                  | architecture §12                                                 |
| Privacy / Law 25                        | architecture §16                                                 |
| Audit + crypto-shredding                | architecture §17                                                 |
| Uploads, public surface                 | architecture §18, §18A                                           |
| Pilot scope                             | product-spec §41                                                 |
| A specific module                       | `docs/modules/<module>.md` — **read this before exploring code** |
| Past decisions                          | `docs/adr/`                                                      |

## Hard rules

1. Every tenant-owned table has `TenantId`, an RLS policy, and indexes/unique constraints starting with `TenantId`. Run `/tenancy-check` on any new entity.
2. `TenantId` is never taken from a request body. It comes from `TenantContext`.
3. Authorization is server-side, permission-based, default deny.
4. Personal fields are classified for audit (crypto-shredding). No personal data in logs or telemetry.
5. User-facing reference data is bilingual (`fr-CA` + `en-CA`). No hard-coded UI strings. French is the default.
6. Québec-specific behaviour lives in rule packs/compliance layer, never `if (province == "QC")` in generic domain code.
7. Modules never touch another module's internals or tables. Use contracts, events, IDs.
8. No new infrastructure (Redis, queues, search engines, Next.js, auth server) without an ADR.
9. No new NuGet/npm dependency without asking first.
10. Never edit an applied migration. Never edit generated code. Never run migrations against a non-local database.
11. AI features, public API, generic engines: out of scope unless an issue says otherwise.

## How to work

1. **Scope = the issue.** The specs are direction, not a to-do list. Build only what the issue's acceptance criteria require. If you think something else is needed, say so and stop.
2. **Plan first.** Read: this file → relevant nested `CLAUDE.md` → `docs/modules/<module>.md` → the doc sections the issue cites. Then propose a plan (files to add/change, tests, migrations). Wait for approval.
3. **Don't crawl.** Do not scan the whole repo to "understand" it. If a module doc is missing or wrong, say so.
4. **Tests first for invariants:** tenant isolation, permission denial, domain rules. Integration tests use real PostgreSQL (Testcontainers), never in-memory or SQLite.
5. **Done means green:** `dotnet build`, `dotnet test`, `pnpm lint`, `pnpm test`, and `pnpm api:generate` with no diff. Never claim done without running them.
6. **New entity or table →** `/tenancy-check`. Persistence or personal-data changes → ask for the `tenancy-privacy-reviewer` agent on the diff.
7. **Finish with `/close-task`:** update module doc, check acceptance criteria, draft commit/PR text.
8. **Decisions →** `/adr`. If you made a choice a future session would need to know, it goes in an ADR or module doc, not only in chat.
9. **French:** write Québec French (e.g. _courriel_, _famille d'accueil_, _médaille_, _stérilisation_). Mark any term you are unsure of with `TODO(fr-review)`.

## Git

- Branch per issue: `m1/rls-set-local`. Conventional commits. Never push to `main`; never force-push.
